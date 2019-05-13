using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Pollster.CommonCode;

namespace Pollster.PollVoter
{
    public class VoterProcessor
    {
        static VoterProcessor _instance;

        public static VoterProcessor Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new VoterProcessor();

                return _instance;
            }
        }


        IAmazonDynamoDB _dynamoDBClient;

        VoterProcessor()
        {
            this._dynamoDBClient = new AmazonDynamoDBClient();
        }

        /// <summary>
        /// Increment the vote count for an option and return back the latest voting results for poll
        /// </summary>
        /// <param name="id"></param>
        /// <param name="optionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string,int>> SubmitVoteAsync(string id, string optionId)
        {
            var request = new UpdateItemRequest
            {
                TableName = "PollDefinition",
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
    }
}
