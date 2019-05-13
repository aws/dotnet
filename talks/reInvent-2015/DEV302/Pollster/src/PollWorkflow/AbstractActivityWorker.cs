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
    public abstract class AbstractActivityWorker
    {
        protected abstract Task ProcessTaskAsync(ActivityTask task);
        protected abstract string ActivityType { get; }
        protected abstract string ActivityTaskList { get; }

        IAmazonSimpleWorkflow _swfClient = new AmazonSimpleWorkflowClient();

        CancellationToken _cancellationToken;
        Task _task;

        public Task Start(CancellationToken cancellationToken = default(CancellationToken))
        {
            this._cancellationToken = cancellationToken;
            this._task = Task.Run((Action)this.PollAndProcess);
            return this._task;
        }

        void PollAndProcess()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ActivityTask task = Poll();
                    if (!String.IsNullOrEmpty(task.TaskToken))
                    {
                        ProcessTaskAsync(task).Wait();
                        CompleteTaskAsync(task.TaskToken);
                    }
                }
                catch(AggregateException e)
                {
                    var inner = e.InnerException;
                    Logger.LogMessage("Unknown error while processing activity {0}: {1}\n{2}", this.ActivityType, inner.Message, inner.StackTrace);
                }
                catch (Exception e)
                {
                    Logger.LogMessage("Unknown error while processing activity {0}: {1}\n{2}", this.ActivityType, e.Message, e.StackTrace);
                }
            }

            Logger.LogMessage("Exiting poll for {0} after getting cancel signal", this.ActivityType);
        }

        ActivityTask Poll()
        {
            Logger.LogMessage("Polling for {0} activity task ...", ActivityType);
            PollForActivityTaskRequest request = new PollForActivityTaskRequest()
            {
                Domain = Constants.SWF_DOMAIN,
                TaskList = new TaskList()
                {
                    Name = this.ActivityTaskList
                }
            };
            PollForActivityTaskResponse response = this._swfClient.PollForActivityTaskAsync(request).Result;
            return response.ActivityTask;
        }

        async void CompleteTaskAsync(String taskToken)
        {
            RespondActivityTaskCompletedRequest request = new RespondActivityTaskCompletedRequest()
            {
                TaskToken = taskToken
            };
            RespondActivityTaskCompletedResponse response = await this._swfClient.RespondActivityTaskCompletedAsync(request);
            Logger.LogMessage("{0} Activity task completed.", this.ActivityType);
        }

    }
}
