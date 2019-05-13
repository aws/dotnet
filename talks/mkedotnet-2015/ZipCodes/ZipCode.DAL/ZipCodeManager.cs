using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DataModel;

namespace ZipCode.DAL
{
    public class ZipCodeManager
    {
        static ZipCodeManager _instance = new ZipCodeManager();

        public static ZipCodeManager Instance
        {
            get { return _instance; }
        }

        IAmazonDynamoDB _dbClient;
        DynamoDBContext _context;

        public ZipCodeManager()
        {
            this._dbClient = new AmazonDynamoDBClient(RegionEndpoint.USWest2);
            this._context = new DynamoDBContext(_dbClient);
        }



        public ZipCodeEntity GetZipCode(string zipCode)
        {
            return this._context.Load<ZipCodeEntity>(zipCode);
        }

        public IList<ZipCodeEntity> GetZipCodesForState(string stateAbbrevation)
        {
            var zipCodes = this._context.Query<ZipCodeEntity>(stateAbbrevation.ToUpperInvariant(), new DynamoDBOperationConfig { IndexName = "State" }).ToList();
            return zipCodes;
        }


        #region Setup
        public void InitializeTable(string zipCodeData)
        {
            var listResponse = this._dbClient.ListTables(new ListTablesRequest
            {
                ExclusiveStartTableName = "ZipCode"
            });

            if (listResponse.TableNames.Count == 0)
            {
                var createRequest = new CreateTableRequest
                {
                    TableName = ZipCodeEntity.TableName,
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement
                        {
                            AttributeName = "PostalCode",
                            KeyType = KeyType.HASH
                        }
                    },
                    GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                    {
                        new GlobalSecondaryIndex
                        {
                            IndexName = "State",
                            KeySchema = new List<KeySchemaElement>
                            {
                                new KeySchemaElement
                                {
                                    AttributeName = "StateAbbrevation",
                                    KeyType = KeyType.HASH
                                }
                            },
                            Projection = new Projection
                            {
                                ProjectionType = ProjectionType.ALL
                            },
                            ProvisionedThroughput = new ProvisionedThroughput
                            {
                                ReadCapacityUnits = 10,
                                WriteCapacityUnits = 1
                            }
                        }
                    },
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition
                        {
                            AttributeName = "PostalCode",
                            AttributeType = ScalarAttributeType.S
                        },
                        new AttributeDefinition
                        {
                            AttributeName = "StateAbbrevation",
                            AttributeType = ScalarAttributeType.S
                        }
                    },
                    ProvisionedThroughput = new ProvisionedThroughput
                    {
                        ReadCapacityUnits = 10,
                        WriteCapacityUnits = 1
                    }
                };
                this._dbClient.CreateTable(createRequest);

                DescribeTableResponse describeResponse = null;
                do
                {
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                    describeResponse = this._dbClient.DescribeTable(ZipCodeEntity.TableName);
                } while (describeResponse.Table.TableStatus != TableStatus.ACTIVE);
            }

            using (StreamReader reader = new StreamReader(@"..\..\US-ZipCodes.txt"))
            {
                Console.WriteLine("Begin Upload");
                int count = 0;
                string line;
                HashSet<string> processedCodes = new HashSet<string>();
                var batchWrite = this._context.CreateBatchWrite<ZipCodeEntity>();
                while ((line = reader.ReadLine()) != null)
                {
                    var tokens = line.Split('\t');
                    if (tokens.Length != 12)
                        continue;

                    var code = new ZipCodeEntity
                    {
                        CountryCode = tokens[0],
                        PostalCode = tokens[1],
                        PlaceName = tokens[2],
                        State = tokens[3],
                        StateAbbrevation = tokens[4],
                        City = tokens[5],
                        Latitude = double.Parse(tokens[9]),
                        Longitude = double.Parse(tokens[10])
                    };

                    if (processedCodes.Contains(code.PostalCode))
                        continue;

                    processedCodes.Add(code.PostalCode);

                    batchWrite.AddPutItem(code);
                    count++;
                    if(count % 25 == 0)
                    {
                        batchWrite.Execute();
                        batchWrite = this._context.CreateBatchWrite<ZipCodeEntity>();
                        Console.WriteLine("...Uploaded {0}", count);
                    }
                }

                batchWrite.Execute();
                Console.WriteLine("Upload Complete {0}", count);
            }
        }
        #endregion
    }
}
