using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.DynamoDBv2;
using Pollster.Models;

namespace Pollster.Workflow
{
    public class StreamProcessor
    {
        const string ENV_POLL_STATE_MACHINE = "POLL_STATE_MACHINE";

        IAmazonStepFunctions _stepClient = new AmazonStepFunctionsClient();
        PollManager _manager;
        string _stateMachine;

        public StreamProcessor()
        {
            this._stateMachine = Environment.GetEnvironmentVariable(ENV_POLL_STATE_MACHINE);
            Console.WriteLine($"Using State Machine {this._stateMachine}");

            this._stepClient = new AmazonStepFunctionsClient();
            this._manager = new PollManager(new AmazonDynamoDBClient());
        }

        public async Task ProcessDDBEvent(DynamoDBEvent evnt, ILambdaContext context)
        {
            foreach(var record in evnt.Records)
            {
                var pollId = record.Dynamodb.Keys["Id"].S;
                context.Logger.LogLine($"Processing poll {pollId}");

                var poll = await this._manager.GetPollByIdAsync(pollId);
                if(poll == null)
                {
                    context.Logger.LogLine($"Failed to find poll for id {pollId}");
                    continue;
                }

                if(poll.State != PollDefinition.POLL_STATE_UNSCHEDULE)
                {
                    context.Logger.LogLine($"Poll {poll.Title} is already scheduled");
                    continue;
                }

                context.Logger.LogLine($"Starting state machine execution");
                await this._stepClient.StartExecutionAsync(new StartExecutionRequest
                {
                    Name = Guid.NewGuid().ToString(),
                    StateMachineArn = this._stateMachine,
                    Input = "{\"PollId\" : \"" + poll.Id + "\"}"
                });
            }
        }
    }
}
