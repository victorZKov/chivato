using Azure;
using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Domain.ValueObjects;

namespace Chivato.Infrastructure.Persistence;

/// <summary>
/// Base repository for Azure Table Storage
/// </summary>
public abstract class TableStorageRepository<TEntity, TTableEntity>
    where TEntity : BaseEntity
    where TTableEntity : class, ITableEntity, new()
{
    protected readonly TableClient _tableClient;

    protected TableStorageRepository(TableServiceClient tableServiceClient, string tableName)
    {
        _tableClient = tableServiceClient.GetTableClient(tableName);
        _tableClient.CreateIfNotExists();
    }

    protected abstract TTableEntity ToTableEntity(TEntity entity);
    protected abstract TEntity FromTableEntity(TTableEntity tableEntity);
}

/// <summary>
/// Pipeline repository implementation
/// </summary>
public class PipelineRepository : IPipelineRepository
{
    private readonly TableClient _tableClient;

    public PipelineRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("Pipelines");
        _tableClient.CreateIfNotExists();
    }

    public async Task<Pipeline?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<PipelineTableEntity>(tenantId, id, cancellationToken: ct);
            return MapToDomain(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Pipeline>> GetAllAsync(string tenantId, CancellationToken ct = default)
    {
        var results = new List<Pipeline>();
        await foreach (var entity in _tableClient.QueryAsync<PipelineTableEntity>(e => e.PartitionKey == tenantId, cancellationToken: ct))
        {
            results.Add(MapToDomain(entity));
        }
        return results;
    }

    public async Task<IReadOnlyList<Pipeline>> GetActiveAsync(string tenantId, CancellationToken ct = default)
    {
        var results = new List<Pipeline>();
        await foreach (var entity in _tableClient.QueryAsync<PipelineTableEntity>(
            e => e.PartitionKey == tenantId && e.Status == "Active", cancellationToken: ct))
        {
            results.Add(MapToDomain(entity));
        }
        return results;
    }

    public async Task AddAsync(Pipeline pipeline, CancellationToken ct = default)
    {
        var entity = MapToTable(pipeline);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(Pipeline pipeline, CancellationToken ct = default)
    {
        var entity = MapToTable(pipeline);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        await _tableClient.DeleteEntityAsync(tenantId, id, cancellationToken: ct);
    }

    private static PipelineTableEntity MapToTable(Pipeline p) => new()
    {
        PartitionKey = p.TenantId,
        RowKey = p.Id,
        Name = p.Name,
        Organization = p.Organization,
        Project = p.Project,
        RepositoryId = p.RepositoryId,
        Branch = p.Branch,
        TerraformPath = p.TerraformPath,
        SubscriptionId = p.SubscriptionId,
        ResourceGroup = p.ResourceGroup,
        Status = p.Status.ToString(),
        LastScanAt = p.LastScanAt,
        DriftCount = p.DriftCount,
        LastScanCorrelationId = p.LastScanCorrelationId,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private static Pipeline MapToDomain(PipelineTableEntity e)
    {
        // Using reflection to set private properties (in real app, use a factory or constructor)
        var pipeline = (Pipeline)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Pipeline));

        typeof(Pipeline).GetProperty("Id")!.SetValue(pipeline, e.RowKey);
        typeof(Pipeline).GetProperty("TenantId")!.SetValue(pipeline, e.PartitionKey);
        typeof(Pipeline).GetProperty("Name")!.SetValue(pipeline, e.Name);
        typeof(Pipeline).GetProperty("Organization")!.SetValue(pipeline, e.Organization);
        typeof(Pipeline).GetProperty("Project")!.SetValue(pipeline, e.Project);
        typeof(Pipeline).GetProperty("RepositoryId")!.SetValue(pipeline, e.RepositoryId);
        typeof(Pipeline).GetProperty("Branch")!.SetValue(pipeline, e.Branch);
        typeof(Pipeline).GetProperty("TerraformPath")!.SetValue(pipeline, e.TerraformPath);
        typeof(Pipeline).GetProperty("SubscriptionId")!.SetValue(pipeline, e.SubscriptionId);
        typeof(Pipeline).GetProperty("ResourceGroup")!.SetValue(pipeline, e.ResourceGroup);
        typeof(Pipeline).GetProperty("Status")!.SetValue(pipeline, Enum.Parse<PipelineStatus>(e.Status ?? "Active"));
        typeof(Pipeline).GetProperty("LastScanAt")!.SetValue(pipeline, e.LastScanAt);
        typeof(Pipeline).GetProperty("DriftCount")!.SetValue(pipeline, e.DriftCount);
        typeof(Pipeline).GetProperty("LastScanCorrelationId")!.SetValue(pipeline, e.LastScanCorrelationId);
        typeof(BaseEntity).GetProperty("CreatedAt")!.SetValue(pipeline, e.CreatedAt);
        typeof(BaseEntity).GetProperty("UpdatedAt")!.SetValue(pipeline, e.UpdatedAt);

        return pipeline;
    }
}

public class PipelineTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string RepositoryId { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string TerraformPath { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string? Status { get; set; }
    public DateTimeOffset? LastScanAt { get; set; }
    public int DriftCount { get; set; }
    public string? LastScanCorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
