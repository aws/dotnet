using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

using Pollster.CommonCode;

namespace Pollster.PollFeed
{
    public class PollFetcher
    {
        static PollFetcher _instance;

        public static PollFetcher Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PollFetcher();

                return _instance;
            }
        }


        IAmazonDynamoDB _dynamoDBClient;
        DynamoDBContext _dbContext;

        PollFetcher()
        {
            this._dynamoDBClient = new AmazonDynamoDBClient();
            this._dbContext = new DynamoDBContext(this._dynamoDBClient, new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 });
        }

        public async Task<PollDefinition> GetPollByIdAsync(string id)
        {
            return await this._dbContext.LoadAsync<PollDefinition>(id);
        }

        public async Task<List<PollDefinition>> GetPollFeedAsync()
        {
            var batchGet = this._dbContext.CreateBatchGet<PollDefinition>();
            var search = this._dbContext.ScanAsync<ActivePoll>(new List<ScanCondition>());
            foreach (var task in await search.GetRemainingAsync())
            {
                batchGet.AddKey(task.Id);
            }

            await batchGet.ExecuteAsync();
            List<PollDefinition> activePolls = batchGet.Results;

            return activePolls;
        }
    }
}
