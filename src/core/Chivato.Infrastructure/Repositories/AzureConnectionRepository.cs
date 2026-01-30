using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class AzureConnectionRepository : IAzureConnectionRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "AzureConnections";

    public AzureConnectionRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<AzureConnection?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<AzureConnectionTableEntity>(tenantId, id, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<AzureConnection>> GetAllAsync(string tenantId, CancellationToken ct = default)
    {
        var connections = new List<AzureConnection>();
        var filter = $"PartitionKey eq '{tenantId}'";
        var query = _tableClient.QueryAsync<AzureConnectionTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            connections.Add(entity.ToDomain());
        }

        return connections;
    }

    public async Task<AzureConnection?> GetDefaultAsync(string tenantId, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and IsDefault eq true";
        var query = _tableClient.QueryAsync<AzureConnectionTableEntity>(filter: filter, maxPerPage: 1, cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToDomain();
        }

        return null;
    }

    public async Task<AzureConnection?> GetBySubscriptionIdAsync(string tenantId, string subscriptionId, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and SubscriptionId eq '{subscriptionId}'";
        var query = _tableClient.QueryAsync<AzureConnectionTableEntity>(filter: filter, maxPerPage: 1, cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToDomain();
        }

        return null;
    }

    public async Task AddAsync(AzureConnection connection, CancellationToken ct = default)
    {
        var entity = AzureConnectionTableEntity.FromDomain(connection);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(AzureConnection connection, CancellationToken ct = default)
    {
        var entity = AzureConnectionTableEntity.FromDomain(connection);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        await _tableClient.DeleteEntityAsync(tenantId, id, cancellationToken: ct);
    }
}
