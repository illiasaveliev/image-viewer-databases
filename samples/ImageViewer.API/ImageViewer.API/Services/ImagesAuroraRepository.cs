using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.RDSDataService;
using Amazon.RDSDataService.Model;
using ImageViewer.API.Models;
using ExecuteStatementRequest = Amazon.RDSDataService.Model.ExecuteStatementRequest;

namespace ImageViewer.API.Services
{
    public class ImagesAuroraRepository : IImagesRepository
    {
        private readonly string _secretArn;
        private readonly string _auroraServerArn;
        private readonly string _database;

        public ImagesAuroraRepository(string auroraServerArn, string secretArn, string database)
        {
            _auroraServerArn = auroraServerArn;
            _secretArn = secretArn;
            _database = database;
        }

        public async Task<IEnumerable<ImageModel>> GetAllAsync()
        {
            try
            {
                var client = new AmazonRDSDataServiceClient();
                var request = CreateExecuteRequest("select images.*, imageTags.tag, imageTags.value from images left join imageTags on images.id = imageTags.imageId");
                var result = await client.ExecuteStatementAsync(request);

                var items = result.Records.Select(ToImageTagModel).ToList();
                return items.GroupBy(g => new { g.Id, g.Key, g.ETag, g.LastModified, g.Size })
                    .Select(gr => new ImageModel
                    {
                        Id = gr.Key.Id,
                        Key = gr.Key.Key,
                        ETag = gr.Key.ETag,
                        LastModified = gr.Key.LastModified,
                        Size = gr.Key.Size,
                        Tags = gr.Where(t => t.Tag != null).Select(t => new ImageTag { Tag = t.Tag, Value = t.Value }).ToList()
                    }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<ImageModel>();
            }

        }

        public async Task DeleteAsync(string id, string key)
        {
            var client = new AmazonRDSDataServiceClient();

            var transaction = new BeginTransactionRequest
            {
                SecretArn = _secretArn,
                ResourceArn = _auroraServerArn,
                Database = _database
            };

            var transactionResponse = await client.BeginTransactionAsync(transaction);

            try
            {
                ExecuteStatementRequest deleteTagsRequest = new ExecuteStatementRequest()
                {
                    SecretArn = _secretArn,
                    ResourceArn = _auroraServerArn,
                    Parameters = BuildParams(new Dictionary<string, string>()
                {
                    { "imageId", key }
                }),
                    Database = _database,
                    Sql = "delete from imageTags where imageId = :imageId",
                    TransactionId = transactionResponse.TransactionId
                };
                await client.ExecuteStatementAsync(deleteTagsRequest);

                ExecuteStatementRequest deleteImageRequest = new ExecuteStatementRequest()
                {
                    SecretArn = _secretArn,
                    ResourceArn = _auroraServerArn,
                    Parameters = BuildParams(new Dictionary<string, string>()
                {
                    { "id", key }
                }),
                    Database = _database,
                    Sql = "delete from images where id = :id",
                    TransactionId = transactionResponse.TransactionId
                };
                await client.ExecuteStatementAsync(deleteImageRequest);

                var commitRequest = new CommitTransactionRequest {
                    SecretArn = _secretArn,
                    ResourceArn = _auroraServerArn,
                    TransactionId =transactionResponse.TransactionId
                };
                await client.CommitTransactionAsync(commitRequest);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                var rollbackRequest = new RollbackTransactionRequest
                {
                    SecretArn = _secretArn,
                    ResourceArn = _auroraServerArn,
                    TransactionId = transactionResponse.TransactionId
                };
                await client.RollbackTransactionAsync(rollbackRequest);
            }
        }


        private ExecuteStatementRequest CreateExecuteRequest(string sqlCommand, Dictionary<string, string> parameters = null)
        {
            ExecuteStatementRequest executeStatementRequest = new ExecuteStatementRequest()
            {
                SecretArn = _secretArn,
                ResourceArn = _auroraServerArn,
                IncludeResultMetadata = true,
                ContinueAfterTimeout = true,
                Database = _database,
                Sql = sqlCommand
            };
            if (parameters != null && parameters.Count > 0)
                executeStatementRequest.Parameters.AddRange(BuildParams(parameters));
            return executeStatementRequest;
        }

        private List<SqlParameter> BuildParams(Dictionary<string, string> parameters)
        {
            var sqlParameters = parameters.Select(p => new SqlParameter { Name = p.Key, Value = new Field() { StringValue = p.Value, IsNull = string.IsNullOrEmpty(p.Value) } }).ToList();
            return sqlParameters;
        }

        private static ImageWithTagInfo ToImageTagModel(List<Field> record)
        {
            return new ImageWithTagInfo
            {
                Id = record[0].LongValue.ToString(),
                Key = record[1].StringValue,
                ETag = record[2].StringValue,
                LastModified = DateTime.Parse(record[3].StringValue),
                Size = record[4].LongValue,
                Tag = record[5].StringValue,
                Value = record[6].StringValue,
            };
        }
    }

    internal class ImageWithTagInfo
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string ETag { get; set; }
        public string Tag { get; set; }
        public string Value { get; set; }
    }
}
