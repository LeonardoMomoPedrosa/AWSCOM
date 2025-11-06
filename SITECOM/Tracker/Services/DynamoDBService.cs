using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Tracker.Models;
using System.Collections.Generic;

namespace Tracker.Services;

public class DynamoDBService
{
    private readonly AmazonDynamoDBClient _client;
    private readonly string _tableName;

    public DynamoDBService(string tableName, string region)
    {
        _tableName = tableName;
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        _client = new AmazonDynamoDBClient(regionEndpoint);
    }

    public async Task<List<TrackingRecord>> ScanAllItemsAsync()
    {
        var items = new List<TrackingRecord>();
        var request = new ScanRequest
        {
            TableName = _tableName
        };

        ScanResponse response;
        do
        {
            response = await _client.ScanAsync(request);

            foreach (var item in response.Items)
            {
                items.Add(ConvertFromDynamoDBItem(item));
            }

            request.ExclusiveStartKey = response.LastEvaluatedKey;
        } while (response.LastEvaluatedKey.Count > 0);

        return items;
    }

    public async Task<TrackingRecord?> GetItemAsync(string idPedido)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "id_pedido", new AttributeValue { S = idPedido } }
            }
        };

        var response = await _client.GetItemAsync(request);

        if (!response.Item.Any())
        {
            return null;
        }

        return ConvertFromDynamoDBItem(response.Item);
    }

    public async Task PutItemAsync(TrackingRecord record)
    {
        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = ConvertToDynamoDBItem(record)
        };

        await _client.PutItemAsync(request);
    }

    public async Task UpdateItemAsync(string idPedido, string rastreamentoJson)
    {
        var request = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "id_pedido", new AttributeValue { S = idPedido } }
            },
            UpdateExpression = "SET rastreamento_json = :json",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":json", new AttributeValue { S = rastreamentoJson } }
            }
        };

        await _client.UpdateItemAsync(request);
    }

    public async Task DeleteItemAsync(string idPedido)
    {
        var request = new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "id_pedido", new AttributeValue { S = idPedido } }
            }
        };

        await _client.DeleteItemAsync(request);
    }

    private TrackingRecord ConvertFromDynamoDBItem(Dictionary<string, AttributeValue> item)
    {
        return new TrackingRecord
        {
            IdPedido = item.ContainsKey("id_pedido") ? item["id_pedido"].S : string.Empty,
            TipoEnvio = item.ContainsKey("tipo_envio") ? item["tipo_envio"].S : string.Empty,
            CodRastreamento = item.ContainsKey("cod_rastreamento") ? item["cod_rastreamento"].S : string.Empty,
            RastreamentoJson = item.ContainsKey("rastreamento_json") ? item["rastreamento_json"].S : string.Empty,
            DataCriacao = item.ContainsKey("data_criacao") ? item["data_criacao"].S : string.Empty,
            Email = item.ContainsKey("email") ? item["email"].S : string.Empty,
            Nome = item.ContainsKey("nome") ? item["nome"].S : string.Empty
        };
    }

    //Convert
    private Dictionary<string, AttributeValue> ConvertToDynamoDBItem(TrackingRecord record)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "id_pedido", new AttributeValue { S = record.IdPedido } }
        };

        if (!string.IsNullOrEmpty(record.TipoEnvio))
        {
            item["tipo_envio"] = new AttributeValue { S = record.TipoEnvio };
        }

        if (!string.IsNullOrEmpty(record.CodRastreamento))
        {
            item["cod_rastreamento"] = new AttributeValue { S = record.CodRastreamento };
        }

        if (!string.IsNullOrEmpty(record.RastreamentoJson))
        {
            item["rastreamento_json"] = new AttributeValue { S = record.RastreamentoJson };
        }

        if (!string.IsNullOrEmpty(record.DataCriacao))
        {
            item["data_criacao"] = new AttributeValue { S = record.DataCriacao };
        }
        else
        {
            item["data_criacao"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
        }

        if (!string.IsNullOrEmpty(record.Email))
        {
            item["email"] = new AttributeValue { S = record.Email };
        }

        if (!string.IsNullOrEmpty(record.Nome))
        {
            item["nome"] = new AttributeValue { S = record.Nome };
        }

        return item;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

