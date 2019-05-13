using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;

using Amazon.SimpleNotificationService;

using Pollster.CommonCode;

namespace Pollster.PollWriter
{
    public class PollWriterManager
    {
        static PollWriterManager _instance;

        public static PollWriterManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PollWriterManager();
                    _instance._swfWorkflowTypeVersion = _instance.GetLatestWorkflowTypeVersion();
                }

                return _instance;
            }
        }


        IAmazonSimpleWorkflow _swfClient;
        IAmazonSimpleNotificationService _snsClient;
        IAmazonDynamoDB _dynamoDBClient;
        DynamoDBContext _dbContext;

        string _swfWorkflowTypeVersion;

        PollWriterManager()
        {
            this._snsClient = new AmazonSimpleNotificationServiceClient();

            this._dynamoDBClient = new AmazonDynamoDBClient();
            this._dbContext = new DynamoDBContext(this._dynamoDBClient, new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 });

            this._swfClient = new AmazonSimpleWorkflowClient();
        }

        private string GetLatestWorkflowTypeVersion()
        {
            var request = new ListWorkflowTypesRequest
            {
                Domain = Constants.SWF_DOMAIN,
                Name = Constants.SWF_WORKFLOW_TYPE_NAME,
                RegistrationStatus = RegistrationStatus.REGISTERED
            };
            var listResponse = _instance._swfClient.ListWorkflowTypesAsync(request).Result;
            return listResponse.WorkflowTypeInfos.TypeInfos[0].WorkflowType.Version;
        }

        public async Task<string> SavePollAsync(PollDefinition poll)
        {
            if (string.IsNullOrEmpty(poll.Id))
            {
                poll.Id = Guid.NewGuid().ToString();

                var topic = (await this._snsClient.CreateTopicAsync("pollster-poll-" + poll.Id)).TopicArn;
                poll.TopicArn = topic;
                await this._dbContext.SaveAsync<PollDefinition>(poll);

                await this._snsClient.SubscribeAsync(topic, "email", poll.AuthorEmail);

            }
            await this._dbContext.SaveAsync(poll);

            await this._swfClient.StartWorkflowExecutionAsync(new StartWorkflowExecutionRequest
            {
                Domain = Constants.SWF_DOMAIN,
                WorkflowId = poll.Id,
                WorkflowType = new WorkflowType()
                {
                    Name = Constants.SWF_WORKFLOW_TYPE_NAME,
                    Version = _swfWorkflowTypeVersion,
                },
                TaskList = new TaskList
                {
                    Name = Constants.SWF_DECIDER_TASKLIST
                },
                Input = poll.Id
            });

            return poll.Id;
        }

        public async Task DeletePollAsync(string id)
        {
            await this._dbContext.DeleteAsync<PollDefinition>(id);
        }

        public async Task<PollDefinition> GetPollByIdAsync(string id)
        {
            return await this._dbContext.LoadAsync<PollDefinition>(id);
        }

        public async Task<List<PollDefinition>> GetPollsByAuthorAsync(string authorEmail)
        {
            List<PollDefinition> polls = new List<PollDefinition>();
            var search = this._dbContext.QueryAsync<PollDefinition>(authorEmail, new DynamoDBOperationConfig {IndexName = "AuthorEmail" });
            foreach(var poll in await search.GetRemainingAsync())
            {
                polls.Add(poll);
            }

            return polls;
        }
    }
}
