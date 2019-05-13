using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon;

using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;

using Pollster.CommonCode;

namespace Pollster.PollWorkflow
{
    public class WorkflowDecider
    {
        IAmazonSimpleWorkflow _swfClient = new AmazonSimpleWorkflowClient();

        CancellationToken _cancellationToken;
        Task _task;

        public Task Start(CancellationToken cancellationToken = default(CancellationToken))
        {
            this._cancellationToken = cancellationToken;
            this._task = Task.Run((Action)this.PollAndDecide);
            return this._task;
        }

        void PollAndDecide()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    DecisionTask task = Poll();
                    if (!string.IsNullOrEmpty(task.TaskToken))
                    {
                        //Create the next set of decision based on the current state and 
                        //the execution history
                        var decisions = Decide(task);

                        //Complete the task with the new set of decisions
                        CompleteTaskAsync(task.TaskToken, decisions).Wait();
                    }
                }
                catch (AggregateException e)
                {
                    var inner = e.InnerException;
                    Logger.LogMessage("Unknown error while processing workflow decisions: {0}\n{1}", inner.Message, inner.StackTrace);
                }
                catch (Exception e)
                {
                    Logger.LogMessage("Unknown error while processing workflow decisions: {0}\n{1}", e.Message, e.StackTrace);
                }
            }
        }

        DecisionTask Poll()
        {
            Logger.LogMessage("Polling for Decision task ...");
            PollForDecisionTaskRequest request = new PollForDecisionTaskRequest()
            {
                Domain = Constants.SWF_DOMAIN,
                TaskList = new TaskList()
                {
                    Name = Constants.SWF_DECIDER_TASKLIST
                }
            };
            PollForDecisionTaskResponse response = _swfClient.PollForDecisionTaskAsync(request).Result;
            return response.DecisionTask;
        }

        async Task CompleteTaskAsync(string taskToken, List<Decision> decisions)
        {
            RespondDecisionTaskCompletedRequest request = new RespondDecisionTaskCompletedRequest()
            {
                Decisions = decisions,
                TaskToken = taskToken
            };

            await this._swfClient.RespondDecisionTaskCompletedAsync(request);
        }

        List<Decision> Decide(DecisionTask task)
        {
            try
            {
                List<Decision> decisions = new List<Decision>();
                var startEvent = task.Events.FirstOrDefault(x => x.EventType == EventType.WorkflowExecutionStarted);
                if (startEvent != null)
                {
                    var pollId = startEvent.WorkflowExecutionStartedEventAttributes.Input;
                    Logger.LogMessage("Processing decision task for poll id: " + pollId);

                    var poll = PollProcessor.Instance.GetPollAsync(pollId).Result;

                    if (poll != null)
                    {
                        if (poll.State == PollDefinition.POLL_STATE_UNSCHEDULE && DateTime.Now < poll.StartTime)
                        {
                            // Add second to compensate for the rounding
                            var timeDelay = (int)(new TimeSpan(poll.StartTime.Ticks - DateTime.Now.Ticks).TotalSeconds) + 1;
                            var decision = new Decision
                            {
                                DecisionType = DecisionType.StartTimer,
                                StartTimerDecisionAttributes = new StartTimerDecisionAttributes
                                {
                                    StartToFireTimeout = timeDelay.ToString(),
                                    TimerId = Guid.NewGuid().ToString()
                                }
                            };
                            decisions.Add(decision);
                            PollProcessor.Instance.UpdatePollStateAsync(pollId, PollDefinition.POLL_STATE_SCHEDULE).Wait();
                            Logger.LogMessage("Scheduled timer for {0} seconds till activating poll.", timeDelay);
                        }
                        else if (poll.State == PollDefinition.POLL_STATE_SCHEDULE)
                        {
                            Decision decision = new Decision()
                            {
                                DecisionType = DecisionType.ScheduleActivityTask,
                                ScheduleActivityTaskDecisionAttributes = new ScheduleActivityTaskDecisionAttributes()
                                {
                                    ActivityType = new ActivityType()
                                    {
                                        Name = Constants.SWF_ACTIVTY_START_TIMER_EXPIRED,
                                        Version = Constants.SWF_ACTIVTY_START_TIMER_EXPIRED_VERSION
                                    },
                                    TaskList = new TaskList
                                    {
                                        Name = Constants.SWF_ACTIVTY_START_TIMER_EXPIRED_TASKLIST
                                    },
                                    HeartbeatTimeout = Constants.SWF_TIMEOUT,
                                    ScheduleToCloseTimeout = Constants.SWF_TIMEOUT,
                                    ScheduleToStartTimeout = Constants.SWF_TIMEOUT,
                                    StartToCloseTimeout = Constants.SWF_TIMEOUT,
                                    ActivityId = string.Format("{0}:{1}", Constants.SWF_ACTIVTY_START_TIMER_EXPIRED, DateTime.Now.Ticks),
                                    Input = poll.Id
                                }
                            };
                            decisions.Add(decision);
                            Logger.LogMessage("Start timer complete now deciding to run the {0} activity to activate poll.", Constants.SWF_ACTIVTY_START_TIMER_EXPIRED);
                        }
                        else if (poll.State == PollDefinition.POLL_STATE_ACTIVE && DateTime.Now < poll.EndTime)
                        {
                            // Add second to compensate for the rounding
                            var timeDelay = (int)(new TimeSpan(poll.EndTime.Ticks - DateTime.Now.Ticks).TotalSeconds) + 1;
                            var decision = new Decision
                            {
                                DecisionType = DecisionType.StartTimer,
                                StartTimerDecisionAttributes = new StartTimerDecisionAttributes
                                {
                                    StartToFireTimeout = timeDelay.ToString(),
                                    TimerId = Guid.NewGuid().ToString()
                                }
                            };
                            decisions.Add(decision);
                            Logger.LogMessage("Scheduled timer for {0} seconds till poll expires.", timeDelay);
                        }
                        else if (poll.State == PollDefinition.POLL_STATE_ACTIVE)
                        {
                            Decision decision = new Decision()
                            {
                                DecisionType = DecisionType.ScheduleActivityTask,
                                ScheduleActivityTaskDecisionAttributes = new ScheduleActivityTaskDecisionAttributes()
                                {
                                    ActivityType = new ActivityType()
                                    {
                                        Name = Constants.SWF_ACTIVTY_END_TIMER_EXPIRED,
                                        Version = Constants.SWF_ACTIVTY_END_TIMER_EXPIRED_VERSION
                                    },
                                    TaskList = new TaskList
                                    {
                                        Name = Constants.SWF_ACTIVTY_END_TIMER_EXPIRED_TASKLIST
                                    },
                                    HeartbeatTimeout = Constants.SWF_TIMEOUT,
                                    ScheduleToCloseTimeout = Constants.SWF_TIMEOUT,
                                    ScheduleToStartTimeout = Constants.SWF_TIMEOUT,
                                    StartToCloseTimeout = Constants.SWF_TIMEOUT,
                                    ActivityId = string.Format("{0}:{1}", Constants.SWF_ACTIVTY_END_TIMER_EXPIRED, DateTime.Now.Ticks),
                                    Input = poll.Id
                                }
                            };
                            decisions.Add(decision);
                            Logger.LogMessage("End timer complete now deciding to run the {0} activity to expire poll.", Constants.SWF_ACTIVTY_END_TIMER_EXPIRED);
                        }
                    }
                }

                if (decisions.Count == 0)
                {
                    Decision decision = new Decision()
                    {
                        DecisionType = DecisionType.CompleteWorkflowExecution,
                        CompleteWorkflowExecutionDecisionAttributes = new CompleteWorkflowExecutionDecisionAttributes
                        {
                        }
                    };
                    decisions.Add(decision);
                    Logger.LogMessage("Workflow execution complete for {0}", task.WorkflowExecution.WorkflowId);
                }

                return decisions;
            }
            catch(Exception e)
            {
                string message = string.Format("Error processing work flow execution {0} and is being aborted: {1}\n", task.WorkflowExecution.WorkflowId, e.Message, e.StackTrace);
                Logger.LogMessage(message);
                Decision decision = new Decision()
                {
                    DecisionType = DecisionType.CompleteWorkflowExecution,
                    CompleteWorkflowExecutionDecisionAttributes = new CompleteWorkflowExecutionDecisionAttributes
                    {
                        Result = message
                    }
                };
                return new List<Decision> { decision };                
            }
        }

    }
}
