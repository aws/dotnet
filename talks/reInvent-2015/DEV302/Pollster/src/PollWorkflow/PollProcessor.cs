using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Pollster.CommonCode;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Pollster.PollWorkflow
{
    public class PollProcessor
    {
        static PollProcessor _instance;

        public static PollProcessor Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PollProcessor();

                return _instance;
            }
        }

        IAmazonDynamoDB _dynamoDBClient;
        IAmazonSimpleNotificationService _snsClient;
        DynamoDBContext _dbContext;

        private PollProcessor()
        {
            this._snsClient = new AmazonSimpleNotificationServiceClient();
            this._dynamoDBClient = new AmazonDynamoDBClient();
            this._dbContext = new DynamoDBContext(this._dynamoDBClient, new DynamoDBContextConfig {Conversion = DynamoDBEntryConversion.V2 });
        }

        public async Task<PollDefinition> GetPollAsync(string id)
        {
            return await this._dbContext.LoadAsync<PollDefinition>(id);
        }

        public async Task UpdatePollStateAsync(string id, string state)
        {
            UpdateItemRequest updateRequest = new UpdateItemRequest
            {
                TableName = "PollDefinition",
                Key = new Dictionary<string, AttributeValue>
                {
                    {"Id", new AttributeValue {S = id } }
                },
                AttributeUpdates = new Dictionary<string, AttributeValueUpdate>
                {
                    {"State", new AttributeValueUpdate
                        {
                            Value = new AttributeValue{ S =  state },
                            Action = AttributeAction.PUT
                        }
                    }
                }
            };

            await this._dynamoDBClient.UpdateItemAsync(updateRequest);
        }

        public async Task ActivatePollAsync(string id)
        {
            var poll = await this._dbContext.LoadAsync<PollDefinition>(id);

            var activeRecord = new ActivePoll
            {
                Id = id,
                ActivatedTime = DateTime.Now
            };

            await _dbContext.SaveAsync<ActivePoll>(activeRecord);
            await UpdatePollStateAsync(id, PollDefinition.POLL_STATE_ACTIVE);

            var message = string.Format("Poll {0} was activated at {1} and will last until {2}", poll.Title, poll.StartTime, poll.EndTime);
            await this._snsClient.PublishAsync(poll.TopicArn, message, string.Format("Pollster Poll {0} Activated", poll.Title));
        }

        public async Task DeactivatePoll(string id)
        {
            await _dbContext.DeleteAsync<ActivePoll>(id);
            await UpdatePollStateAsync(id, PollDefinition.POLL_STATE_EXPIRED);

            var poll = await this._dbContext.LoadAsync<PollDefinition>(id);

            var message = new StringBuilder();
            message.AppendFormat("Poll {0} has expired, final results are:\n", poll.Title);
            foreach(var option in poll.Options.Values.OrderByDescending(x => x.Votes))
            {
                message.AppendFormat("\t{0}: {1} Votes\n", option.Text, option.Votes);
            }
            await this._snsClient.PublishAsync(poll.TopicArn,
                message.ToString(),
                string.Format("Poll {0} has expired", poll.Title));
        }
    }
}
