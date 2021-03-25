using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.RDSDataService;
using Amazon.RDSDataService.Model;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ImageViewer.Labeling
{
    public class Function
    {
        /// <summary>
        /// The default minimum confidence used for detecting labels.
        /// </summary>
        public const float DEFAULT_MIN_CONFIDENCE = 70f;

        /// <summary>
        /// The name of the environment variable to set which will override the default minimum confidence level.
        /// </summary>
        public const string MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME = "MinConfidence";
        
        IAmazonRekognition RekognitionClient { get; }

        float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

        HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".jpg", ".jpeg" };
        private string imagesTable;
        private string auroraArn;
        private string auroraSecretArn;
        private string databaseName;

        /// <summary>
        /// Default constructor used by AWS Lambda to construct the function. Credentials and Region information will
        /// be set by the running Lambda environment.
        /// 
        /// This constuctor will also search for the environment variable overriding the default minimum confidence level
        /// for label detection.
        /// </summary>
        public Function()
        {
            this.RekognitionClient = new AmazonRekognitionClient();

            var environmentMinConfidence = System.Environment.GetEnvironmentVariable(MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME);
            imagesTable = Environment.GetEnvironmentVariable("DynamoDbImagesTableName");
            auroraArn = Environment.GetEnvironmentVariable("AuroraArn");
            auroraSecretArn = Environment.GetEnvironmentVariable("AuroraSecretArn");
            databaseName = Environment.GetEnvironmentVariable("DatabaseName");
            if (!string.IsNullOrWhiteSpace(environmentMinConfidence))
            {
                float value;
                if(float.TryParse(environmentMinConfidence, out value))
                {
                    this.MinConfidence = value;
                    Console.WriteLine($"Setting minimum confidence to {this.MinConfidence}");
                }
                else
                {
                    Console.WriteLine($"Failed to parse value {environmentMinConfidence} for minimum confidence. Reverting back to default of {this.MinConfidence}");
                }
            }
            else
            {
                Console.WriteLine($"Using default minimum confidence of {this.MinConfidence}");
            }
        }

        /// <summary>
        /// Constructor used for testing which will pass in the already configured service clients.
        /// </summary>
        /// <param name="s3Client"></param>
        /// <param name="rekognitionClient"></param>
        /// <param name="minConfidence"></param>
        public Function(IAmazonS3 s3Client, IAmazonRekognition rekognitionClient, float minConfidence)
        {
            this.RekognitionClient = rekognitionClient;
            this.MinConfidence = minConfidence;
        }

        /// <summary>
        /// A function for responding to S3 create events. It will determine if the object is an image and use Amazon Rekognition
        /// to detect labels and add the labels as tags on the S3 object.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(S3Event input, ILambdaContext context)
        {
            try
            {
                foreach (var record in input.Records)
                {
                    if (!SupportedImageTypes.Contains(Path.GetExtension(record.S3.Object.Key)))
                    {
                        Console.WriteLine($"Object {record.S3.Bucket.Name}:{record.S3.Object.Key} is not a supported image type");
                        continue;
                    }

                    Console.WriteLine($"Looking for labels in image {record.S3.Bucket.Name}:{record.S3.Object.Key}");
                    var detectResponses = await this.RekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
                    {
                        MinConfidence = MinConfidence,
                        Image = new Image
                        {
                            S3Object = new Amazon.Rekognition.Model.S3Object
                            {
                                Bucket = record.S3.Bucket.Name,
                                Name = record.S3.Object.Key
                            }
                        }
                    });

                    var tags = new List<ImageTag>();
                    foreach (var label in detectResponses.Labels)
                    {
                        if (tags.Count < 10)
                        {
                            Console.WriteLine($"\tFound Label {label.Name} with confidence {label.Confidence}");
                            tags.Add(new ImageTag { Tag = label.Name, Value = label.Confidence.ToString() });
                        }
                        else
                        {
                            Console.WriteLine($"\tSkipped label {label.Name} with confidence {label.Confidence} because the maximum number of tags has been reached");
                        }
                    }

                    //await SaveToDynamoDbAsync(record.S3.Object.Key, record.S3.Object.ETag, record.S3.Object.Size, tags);
                    await SaveToAuroraAsync(record.S3.Object.Key, record.S3.Object.ETag, record.S3.Object.Size, tags);
                }
                return;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public async Task SaveToDynamoDbAsync(string key, string eTag, long size, List<ImageTag> tags)
        {
            using var client = new AmazonDynamoDBClient();
            var item = new Dictionary<string, AttributeValue>
                {
                    { "id", new AttributeValue { S = Guid.NewGuid().ToString() } },
                    { "key", new AttributeValue { S = key } },
                    { "etag", new AttributeValue { S = eTag } },
                    { "lastModified", new AttributeValue { S = DateTime.Now.ToString() } },
                    { "size", new AttributeValue { N = size.ToString() } },
                    { "tags", new AttributeValue { M = tags.ToDictionary(t => t.Tag, t => new AttributeValue { N = t.Value}) } },
                };

            await client.PutItemAsync(imagesTable, item);
        }

        private async Task SaveToAuroraAsync(string key, string eTag, long size, List<ImageTag> tags)
        {
            long imageId = await SaveImageToAuroraAsync(key, eTag, size);
            
            if (imageId > 0)
            {
                await SaveTagsToAuroraAsync(imageId, tags);
            }
        }

        private async Task<long> SaveImageToAuroraAsync(string key, string eTag, long size)
        {
            var client = new AmazonRDSDataServiceClient();
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                { "s3key", key },
                { "etag", eTag },
                { "lastModified", DateTime.Now.ToString("yyyy-MM-dd") },
                { "size", size.ToString() },
            };

            Amazon.RDSDataService.Model.ExecuteStatementRequest request = new Amazon.RDSDataService.Model.ExecuteStatementRequest()
            {
                SecretArn = auroraSecretArn,
                ResourceArn = auroraArn,
                IncludeResultMetadata = true,
                ContinueAfterTimeout = true,
                Database = databaseName,
                Sql = "INSERT INTO images(s3key,etag, lastModified, Size) VALUES(:s3key,:etag,:lastModified,:size)",
                Parameters = BuildParams(parameters)
            };

            var result = await client.ExecuteStatementAsync(request);

            return result.GeneratedFields.Count > 0 ? result.GeneratedFields[0].LongValue : 0;
        }

        private async Task SaveTagsToAuroraAsync(long imageId, List<ImageTag> tags)
        {
            var client = new AmazonRDSDataServiceClient();
            List<List<SqlParameter>> qparameters = new List<List<SqlParameter>>();
            foreach (ImageTag tag in tags)
            {
                qparameters.Add(BuildParams(new Dictionary<string, string>()
                {
                    { "tag", tag.Tag },
                    { "value", tag.Value },
                    { "imageId", imageId.ToString() }
                }));
            }

            Amazon.RDSDataService.Model.BatchExecuteStatementRequest batchRequest = new Amazon.RDSDataService.Model.BatchExecuteStatementRequest()
            {
                SecretArn = auroraSecretArn,
                ResourceArn = auroraArn,
                Database = databaseName,
                ParameterSets = qparameters,
                Sql = "INSERT INTO imageTags(imageId, tag, value) VALUES(:imageId,:tag,:value)"
            };

            await client.BatchExecuteStatementAsync(batchRequest);
        }

        private List<SqlParameter> BuildParams(Dictionary<string, string> parameters)
        {
            return parameters.Select(p => new SqlParameter { Name = p.Key, Value = new Field() { StringValue = p.Value, IsNull = string.IsNullOrEmpty(p.Value) } }).ToList();
        }

    }
}
