using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class PipelineRepository : IPipelineRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "Pipelines";

    public PipelineRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<Pipeline?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<PipelineTableEntity>(tenantId, id, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Pipeline>> GetAllAsync(string tenantId, CancellationToken ct = default)
    {
        var pipelines = new List<Pipeline>();
        var query = _tableClient.QueryAsync<PipelineTableEntity>(
            filter: $"PartitionKey eq '{tenantId}'",
            cancellationToken: ct);

        await foreach (var entity in query)
        {
            pipelines.Add(entity.ToDomain());
        }

        return pipelines;
    }

    public async Task<IReadOnlyList<Pipeline>> GetActiveAsync(string tenantId, CancellationToken ct = default)
    {
        var pipelines = new List<Pipeline>();
        var query = _tableClient.QueryAsync<PipelineTableEntity>(
            filter: $"PartitionKey eq '{tenantId}' and Status eq 'Active'",
            cancellationToken: ct);

        await foreach (var entity in query)
        {
            pipelines.Add(entity.ToDomain());
        }

        return pipelines;
    }

    public async Task AddAsync(Pipeline pipeline, CancellationToken ct = default)
    {
        var entity = PipelineTableEntity.FromDomain(pipeline);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(Pipeline pipeline, CancellationToken ct = default)
    {
        var entity = PipelineTableEntity.FromDomain(pipeline);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        await _tableClient.DeleteEntityAsync(tenantId, id, cancellationToken: ct);
    }
}
