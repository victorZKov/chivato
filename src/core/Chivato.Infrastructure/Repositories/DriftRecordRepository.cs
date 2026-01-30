using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class DriftRecordRepository : IDriftRecordRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "DriftRecords";

    public DriftRecordRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<DriftRecord?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<DriftRecordTableEntity>(tenantId, id, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<PagedResult<DriftRecord>> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? severity = null,
        string? pipelineId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}'";

        if (!string.IsNullOrEmpty(severity))
            filter += $" and Severity eq '{severity}'";

        if (!string.IsNullOrEmpty(pipelineId))
            filter += $" and PipelineId eq '{pipelineId}'";

        if (from.HasValue)
            filter += $" and DetectedAt ge datetime'{from.Value:O}'";

        if (to.HasValue)
            filter += $" and DetectedAt le datetime'{to.Value:O}'";

        var allRecords = new List<DriftRecord>();
        var query = _tableClient.QueryAsync<DriftRecordTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            allRecords.Add(entity.ToDomain());
        }

        // Sort by DetectedAt descending
        var ordered = allRecords.OrderByDescending(d => d.DetectedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<DriftRecord>(items, total, page, pageSize);
    }

    public async Task<DriftStats> GetStatsAsync(string tenantId, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and Status eq 'Open'";
        var query = _tableClient.QueryAsync<DriftRecordTableEntity>(filter: filter, cancellationToken: ct);

        int total = 0, critical = 0, high = 0, medium = 0, low = 0;
        DateTimeOffset? lastAnalysis = null;

        await foreach (var entity in query)
        {
            total++;
            switch (entity.Severity)
            {
                case "Critical": critical++; break;
                case "High": high++; break;
                case "Medium": medium++; break;
                case "Low": low++; break;
            }

            if (!lastAnalysis.HasValue || entity.DetectedAt > lastAnalysis)
                lastAnalysis = entity.DetectedAt;
        }

        return new DriftStats(total, critical, high, medium, low, lastAnalysis);
    }

    public async Task<IReadOnlyList<DriftRecord>> GetByCorrelationIdAsync(string tenantId, string correlationId, CancellationToken ct = default)
    {
        var records = new List<DriftRecord>();
        var filter = $"PartitionKey eq '{tenantId}' and CorrelationId eq '{correlationId}'";
        var query = _tableClient.QueryAsync<DriftRecordTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            records.Add(entity.ToDomain());
        }

        return records;
    }

    public async Task AddAsync(DriftRecord drift, CancellationToken ct = default)
    {
        var entity = DriftRecordTableEntity.FromDomain(drift);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task AddRangeAsync(IEnumerable<DriftRecord> drifts, CancellationToken ct = default)
    {
        var batch = new List<TableTransactionAction>();

        foreach (var drift in drifts)
        {
            var entity = DriftRecordTableEntity.FromDomain(drift);
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, entity));

            // Azure Table Storage batch limit is 100 operations
            if (batch.Count >= 100)
            {
                await _tableClient.SubmitTransactionAsync(batch, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await _tableClient.SubmitTransactionAsync(batch, ct);
        }
    }

    public async Task UpdateAsync(DriftRecord drift, CancellationToken ct = default)
    {
        var entity = DriftRecordTableEntity.FromDomain(drift);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }
}
