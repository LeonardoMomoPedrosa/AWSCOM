using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Personalize.Models;
using System.Text.Json;

namespace Personalize.Services;

public class DynamoDBService : IDisposable
{
    private readonly AmazonDynamoDBClient _client;
    private readonly string _tableName;

    public DynamoDBService(string tableName, string region)
    {
        _tableName = tableName;
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        _client = new AmazonDynamoDBClient(regionEndpoint);
    }

    public async Task<RecommendationRecord?> GetRecommendationAsync(string productId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "productId", new AttributeValue { S = productId } }
            }
        };

        var response = await _client.GetItemAsync(request);

        if (!response.Item.Any())
        {
            return null;
        }

        return ConvertFromDynamoDBItem(response.Item);
    }

    public async Task PutRecommendationAsync(RecommendationRecord record)
    {
        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = ConvertToDynamoDBItem(record)
        };

        await _client.PutItemAsync(request);
    }

    public async Task<List<string>> GetAllProductIdsAsync()
    {
        var productIds = new List<string>();
        var request = new ScanRequest
        {
            TableName = _tableName,
            ProjectionExpression = "productId"
        };

        ScanResponse response;
        do
        {
            response = await _client.ScanAsync(request);

            foreach (var item in response.Items)
            {
                if (item.ContainsKey("productId"))
                {
                    productIds.Add(item["productId"].S);
                }
            }

            request.ExclusiveStartKey = response.LastEvaluatedKey;
        } while (response.LastEvaluatedKey.Count > 0);

        return productIds;
    }

    public async Task DeleteRecommendationAsync(string productId)
    {
        var request = new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "productId", new AttributeValue { S = productId } }
            }
        };

        await _client.DeleteItemAsync(request);
    }

    private RecommendationRecord ConvertFromDynamoDBItem(Dictionary<string, AttributeValue> item)
    {
        var record = new RecommendationRecord
        {
            ProductId = item.ContainsKey("productId") ? item["productId"].S : string.Empty,
            LastUpdated = item.ContainsKey("lastUpdated") 
                ? DateTime.Parse(item["lastUpdated"].S) 
                : DateTime.UtcNow
        };

        if (item.ContainsKey("data"))
        {
            var dataJson = item["data"].S;
            var recommendedProducts = JsonSerializer.Deserialize<List<RecommendedProduct>>(dataJson);
            if (recommendedProducts != null)
            {
                record.RecommendedProducts = recommendedProducts;
            }
        }

        return record;
    }

    private Dictionary<string, AttributeValue> ConvertToDynamoDBItem(RecommendationRecord record)
    {
        var dataJson = JsonSerializer.Serialize(record.RecommendedProducts);

        return new Dictionary<string, AttributeValue>
        {
            { "productId", new AttributeValue { S = record.ProductId } },
            { "data", new AttributeValue { S = dataJson } },
            { "lastUpdated", new AttributeValue { S = record.LastUpdated.ToString("yyyy-MM-ddTHH:mm:ssZ") } }
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

