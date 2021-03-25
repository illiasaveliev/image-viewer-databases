using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImageViewer.TopTags
{
    public class Function
    {
        private readonly string topTagsTable;

        public Function()
        {
            topTagsTable = Environment.GetEnvironmentVariable("TopTagsTable");
        }
        
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(DynamoDBEvent input, ILambdaContext context)
        {
            context.Logger.LogLine("Processing new tags.");
            Dictionary<string, int> newTags = new Dictionary<string, int>();
            foreach(var record in input.Records.Where(r => r.EventName == "INSERT"))
            {
                List<string> tags = record.Dynamodb.NewImage["tags"].M.Select(a => a.Key).ToList();

                foreach (string tag in tags)
                {
                    if (newTags.ContainsKey(tag))
                    {
                        newTags[tag] = newTags[tag] + 1; 
                    }
                    else
                    {
                        newTags.Add(tag, 1);
                    }
                }
            }

            context.Logger.LogLine($"Collected new tags: {newTags.Count}");

            using var client = new AmazonDynamoDBClient();
            foreach (var tag in newTags)
            {
                try
                {
                    var update = new UpdateItemRequest
                    {
                        TableName = topTagsTable,
                        Key = new Dictionary<string, AttributeValue> { { "tag", new AttributeValue { S = tag.Key } } },
                        ExpressionAttributeNames = new Dictionary<string, string>() { { "#T", "total" } },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":sum", new AttributeValue { N = tag.Value.ToString() } } },
                        UpdateExpression = "SET #T = #T + :sum",
                    };

                    await client.UpdateItemAsync(update);
                }
                catch(Exception ex)
                {
                    context.Logger.LogLine(ex.Message);
                    var item = new Dictionary<string, AttributeValue> {
                        { "tag", new AttributeValue { S = tag.Key } },
                        { "total", new AttributeValue { N = tag.Value.ToString() } }
                    };

                    await client.PutItemAsync(topTagsTable, item);
                }
            }

            context.Logger.LogLine("Totals were updated!");
        }
    }
}
