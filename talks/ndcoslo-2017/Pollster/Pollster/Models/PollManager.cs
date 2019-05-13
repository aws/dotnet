using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pollster.Models
{
    public class PollManager
    {
        const string ENV_TABLE_NAME_POLL_DEFINITION = "TABLE_POLL_DEFINITION";
        const string ENV_TABLE_NAME_ACTIVE_POLL = "TABLE_ACTIVE_POLL";

        string PollDefinitionTableName { get; set; } = "PollDefinition";
        string ActivePollTableName { get; set; } = "ActivePoll";

        IAmazonDynamoDB _dynamoDBClient;
        DynamoDBContext _dbContext;

        public PollManager(IAmazonDynamoDB dynamoDBClient)
        {
            // Check to see if a table name was passed in through environment variables and if so 
            // add the table mapping.
            var tableName = System.Environment.GetEnvironmentVariable(ENV_TABLE_NAME_POLL_DEFINITION);
            if (!string.IsNullOrEmpty(tableName))
            {
                PollDefinitionTableName = tableName;
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(PollDefinition)] = new Amazon.Util.TypeMapping(typeof(PollDefinition), tableName);
                Console.WriteLine($"Using table {tableName} for PollDefinition");
            }

            tableName = System.Environment.GetEnvironmentVariable(ENV_TABLE_NAME_ACTIVE_POLL);
            if (!string.IsNullOrEmpty(tableName))
            {
                ActivePollTableName = tableName;
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(ActivePoll)] = new Amazon.Util.TypeMapping(typeof(ActivePoll), tableName);
                Console.WriteLine($"Using table {tableName} for ActivePoll");
            }

            this._dynamoDBClient = dynamoDBClient;
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

        public async Task<string> SavePollAsync(PollDefinition poll)
        {
            if (string.IsNullOrEmpty(poll.Id))
            {
                poll.Id = Guid.NewGuid().ToString();
                await this._dbContext.SaveAsync<PollDefinition>(poll);

                return poll.Id;
            }
            else
            {
                await this._dbContext.SaveAsync(poll);

                return poll.Id;
            }
        }

        public async Task DeletePollAsync(string id)
        {
            await this._dbContext.DeleteAsync<PollDefinition>(id);
        }

        /// <summary>
        /// Increment the vote count for an option and return back the latest voting results for poll
        /// </summary>
        /// <param name="id"></param>
        /// <param name="optionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, int>> SubmitVoteAsync(string id, string optionId)
        {
            var request = new UpdateItemRequest
            {
                TableName = this.PollDefinitionTableName,
                Key = new Dictionary<string, AttributeValue>
                    {
                        {"Id", new AttributeValue {S = id } }
                    },
                UpdateExpression = "ADD Options.#id.Votes :increment",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        {":increment", new AttributeValue{N = "1"}}
                    },
                ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        {"#id", optionId }
                    },
                ReturnValues = ReturnValue.ALL_NEW
            };


            var response = await this._dynamoDBClient.UpdateItemAsync(request);

            // Convert the Options attribute to just a dictionary of option id and votes.
            var currentVotes = new Dictionary<string, int>();
            var optionsAttribute = response.Attributes.FirstOrDefault(x => string.Equals(x.Key, "Options", StringComparison.OrdinalIgnoreCase));

            foreach (var optionKvp in optionsAttribute.Value.M)
            {
                var votes = int.Parse(optionKvp.Value.M["Votes"].N);
                currentVotes[optionKvp.Key] = votes;
            }

            return currentVotes;
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
        }

        public async Task DeactivatePollAsync(string id)
        {
            var poll = await this._dbContext.LoadAsync<PollDefinition>(id);

            var activeRecord = new ActivePoll
            {
                Id = id,
                ActivatedTime = DateTime.Now
            };

            await _dbContext.DeleteAsync<ActivePoll>(id);
            await UpdatePollStateAsync(id, PollDefinition.POLL_STATE_EXPIRED);
        }

        public async Task UpdatePollStateAsync(string id, string state)
        {
            UpdateItemRequest updateRequest = new UpdateItemRequest
            {
                TableName = this.PollDefinitionTableName,
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
    }
}
