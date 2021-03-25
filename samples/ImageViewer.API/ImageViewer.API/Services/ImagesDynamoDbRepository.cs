using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ImageViewer.API.Models;

namespace ImageViewer.API.Services
{
    public class ImagesDynamoDbRepository : IImagesRepository
    {
        private readonly string _imagesTable;

        public ImagesDynamoDbRepository(string imagesTable)
        {
            _imagesTable = imagesTable;
        }

        public async Task<IEnumerable<ImageModel>> GetAllAsync()
        {
            if (string.IsNullOrEmpty(_imagesTable))
            {
                return new List<ImageModel>();
            }

            using var client = new AmazonDynamoDBClient();
            var scanRequest = new ScanRequest
            {
                TableName = _imagesTable
                 
            };

            return (await client.ScanAsync(scanRequest)).Items.Select(ToImageModel);
        }

        public async Task DeleteAsync(string id, string key)
        {
            if (string.IsNullOrEmpty(_imagesTable))
            {
                return;
            }

            using var client = new AmazonDynamoDBClient();
            var item = new Dictionary<string, AttributeValue>
                {
                    { "id", new AttributeValue { S = id } },
                    { "key", new AttributeValue { S = key } }
                };

            await client.DeleteItemAsync(_imagesTable, item);
        }

        private static ImageModel ToImageModel(Dictionary<string, AttributeValue> attrDictionary)
        {
            return new ImageModel
            {
                Id = attrDictionary["id"].S,
                Key = attrDictionary["key"].S,
                ETag = attrDictionary["etag"].S,
                LastModified = DateTime.Parse(attrDictionary["lastModified"].S),
                Size = long.Parse(attrDictionary["size"].N),
                Tags = attrDictionary["tags"].M.Select(a => new ImageTag { Tag  = a.Key, Value = a.Value.N.ToString()}).ToList()
            };
        }
    }
}
