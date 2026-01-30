using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class AdoConnectionRepository : IAdoConnectionRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "AdoConnections";

    public AdoConnectionRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<AdoConnection?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<AdoConnectionTableEntity>(tenantId, id, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<AdoConnection>> GetAllAsync(string tenantId, CancellationToken ct = default)
    {
        var connections = new List<AdoConnection>();
        var filter = $"PartitionKey eq '{tenantId}'";
        var query = _tableClient.QueryAsync<AdoConnectionTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            connections.Add(entity.ToDomain());
        }

        return connections;
    }

    public async Task<AdoConnection?> GetDefaultAsync(string tenantId, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and IsDefault eq true";
        var query = _tableClient.QueryAsync<AdoConnectionTableEntity>(filter: filter, maxPerPage: 1, cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToDomain();
        }

        return null;
    }

    public async Task<AdoConnection?> GetByOrganizationAsync(string tenantId, string organization, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and Organization eq '{organization}'";
        var query = _tableClient.QueryAsync<AdoConnectionTableEntity>(filter: filter, maxPerPage: 1, cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToDomain();
        }

        return null;
    }

    public async Task AddAsync(AdoConnection connection, CancellationToken ct = default)
    {
        var entity = AdoConnectionTableEntity.FromDomain(connection);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(AdoConnection connection, CancellationToken ct = default)
    {
        var entity = AdoConnectionTableEntity.FromDomain(connection);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        await _tableClient.DeleteEntityAsync(tenantId, id, cancellationToken: ct);
    }
}
