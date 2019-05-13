using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

using Pollster.Models;
using Amazon.DynamoDBv2;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Pollster.Workflow
{
    public class StateMachineTasks
    {
        PollManager _manager;

        public StateMachineTasks()
        {
            this._manager = new PollManager(new AmazonDynamoDBClient());
        }

        public async Task<PollState> Schedule(PollState state, ILambdaContext context)
        {
            context.Logger.LogLine($"Scheduling poll {state.PollId}");

            if (string.IsNullOrEmpty(state.PollId))
                throw new Exception("State missing poll id");

            var poll = await this._manager.GetPollByIdAsync(state.PollId);
            if(poll == null)
                throw new Exception($"Failed to find poll with poll id {state.PollId}");

            TimeSpan ts = poll.StartTime - DateTime.Now;
            if (ts.TotalSeconds <= 0)
                state.SecondsTillActive = 0;
            else
                state.SecondsTillActive = (long)ts.TotalSeconds;

            context.Logger.LogLine($"Poll {poll.Title} ({poll.Id}) is scheduled to be active in {state.SecondsTillActive} seconds.");
            return state;
        }

        public async Task<PollState> Activate(PollState state, ILambdaContext context)
        {
            context.Logger.LogLine($"Activating poll {state.PollId}");

            if (string.IsNullOrEmpty(state.PollId))
                throw new Exception("State missing poll id");

            var poll = await this._manager.GetPollByIdAsync(state.PollId);
            if (poll == null)
                throw new Exception($"Failed to find poll with poll id {state.PollId}");

            TimeSpan ts = poll.EndTime - DateTime.Now;
            if (ts.TotalSeconds <= 0)
                throw new Exception("End time has already passed before activating the poll");

            state.SecondsTillDeactivate = (long)ts.TotalSeconds;
            await this._manager.ActivatePollAsync(poll.Id);

            context.Logger.LogLine($"Activated poll {poll.Title} ({poll.Title}) which will stay active for {state.SecondsTillDeactivate} seconds.");
            return state;
        }

        public async Task<PollState> Deactivate(PollState state, ILambdaContext context)
        {
            context.Logger.LogLine($"Deactivating poll {state.PollId}");

            if (string.IsNullOrEmpty(state.PollId))
                throw new Exception("State missing poll id");

            var poll = await this._manager.GetPollByIdAsync(state.PollId);
            if (poll == null)
                throw new Exception($"Failed to find poll with poll id {state.PollId}");


            await this._manager.DeactivatePollAsync(poll.Id);

            context.Logger.LogLine($"Deactivated poll {poll.Title} ({poll.Title})");
            return state;
        }
    }
}
