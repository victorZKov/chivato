using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class ScanLogRepository : IScanLogRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "ScanLogs";

    public ScanLogRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<ScanLog?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<ScanLogTableEntity>(tenantId, id, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<ScanLog?> GetByCorrelationIdAsync(string tenantId, string correlationId, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and CorrelationId eq '{correlationId}'";
        var query = _tableClient.QueryAsync<ScanLogTableEntity>(filter: filter, maxPerPage: 1, cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToDomain();
        }

        return null;
    }

    public async Task<PagedResult<ScanLog>> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status = null,
        string? pipelineId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}'";

        if (!string.IsNullOrEmpty(status))
            filter += $" and Status eq '{status}'";

        if (!string.IsNullOrEmpty(pipelineId))
            filter += $" and PipelineId eq '{pipelineId}'";

        if (from.HasValue)
            filter += $" and StartedAt ge datetime'{from.Value:O}'";

        if (to.HasValue)
            filter += $" and StartedAt le datetime'{to.Value:O}'";

        var allRecords = new List<ScanLog>();
        var query = _tableClient.QueryAsync<ScanLogTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            allRecords.Add(entity.ToDomain());
        }

        // Sort by StartedAt descending
        var ordered = allRecords.OrderByDescending(s => s.StartedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<ScanLog>(items, total, page, pageSize);
    }

    public async Task<ScanStats> GetStatsAsync(string tenantId, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}'";
        var query = _tableClient.QueryAsync<ScanLogTableEntity>(filter: filter, cancellationToken: ct);

        int total = 0, success = 0, failed = 0;
        double totalDuration = 0;

        await foreach (var entity in query)
        {
            total++;
            if (entity.Status == "Success")
                success++;
            else if (entity.Status == "Failed")
                failed++;

            totalDuration += entity.DurationSeconds;
        }

        double avgDuration = total > 0 ? totalDuration / total : 0;

        return new ScanStats(total, success, failed, avgDuration);
    }

    public async Task AddAsync(ScanLog scanLog, CancellationToken ct = default)
    {
        var entity = ScanLogTableEntity.FromDomain(scanLog);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(ScanLog scanLog, CancellationToken ct = default)
    {
        var entity = ScanLogTableEntity.FromDomain(scanLog);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }
}
