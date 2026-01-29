using Azure.Data.Tables;
using Chivato.Shared.Models;

namespace Chivato.Shared.Services;

public class StorageService : IStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private const string ConfigTable = "Configurations";
    private const string AzureConnectionsTable = "AzureConnections";
    private const string AdoConnectionsTable = "AdoConnections";
    private const string PipelinesTable = "Pipelines";
    private const string DriftRecordsTable = "DriftRecords";
    private const string ScanLogsTable = "ScanLogs";
    private const string AnalysisStatusTable = "AnalysisStatus";
    private const string EmailRecipientsTable = "EmailRecipients";
    private const string AiConnectionsTable = "AiConnections";
    private const string EmailServiceConfigTable = "EmailServiceConfig";

    public StorageService(string connectionString)
    {
        _tableServiceClient = new TableServiceClient(connectionString);
    }

    private TableClient GetTableClient(string tableName)
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        tableClient.CreateIfNotExists();
        return tableClient;
    }

    // Configurations
    public async Task<string?> GetConfigValueAsync(string key)
    {
        var tableClient = GetTableClient(ConfigTable);
        try
        {
            var entity = await tableClient.GetEntityAsync<ConfigurationEntity>("config", key);
            return entity.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SetConfigValueAsync(string key, string value, string type)
    {
        var tableClient = GetTableClient(ConfigTable);
        var entity = new ConfigurationEntity
        {
            RowKey = key,
            Value = value,
            Type = type
        };
        await tableClient.UpsertEntityAsync(entity);
    }

    // Azure Connections
    public async Task<IEnumerable<AzureConnectionEntity>> GetAzureConnectionsAsync()
    {
        var tableClient = GetTableClient(AzureConnectionsTable);
        var entities = new List<AzureConnectionEntity>();
        await foreach (var entity in tableClient.QueryAsync<AzureConnectionEntity>(e => e.PartitionKey == "azure"))
        {
            entities.Add(entity);
        }
        return entities;
    }

    public async Task<AzureConnectionEntity?> GetAzureConnectionAsync(string id)
    {
        var tableClient = GetTableClient(AzureConnectionsTable);
        try
        {
            var entity = await tableClient.GetEntityAsync<AzureConnectionEntity>("azure", id);
            return entity.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveAzureConnectionAsync(AzureConnectionEntity connection)
    {
        var tableClient = GetTableClient(AzureConnectionsTable);
        await tableClient.UpsertEntityAsync(connection);
    }

    public async Task DeleteAzureConnectionAsync(string id)
    {
        var tableClient = GetTableClient(AzureConnectionsTable);
        await tableClient.DeleteEntityAsync("azure", id);
    }

    // ADO Connections
    public async Task<IEnumerable<AdoConnectionEntity>> GetAdoConnectionsAsync()
    {
        var tableClient = GetTableClient(AdoConnectionsTable);
        var entities = new List<AdoConnectionEntity>();
        await foreach (var entity in tableClient.QueryAsync<AdoConnectionEntity>(e => e.PartitionKey == "ado"))
        {
            entities.Add(entity);
        }
        return entities;
    }

    public async Task<AdoConnectionEntity?> GetAdoConnectionAsync(string id)
    {
        var tableClient = GetTableClient(AdoConnectionsTable);
        try
        {
            var entity = await tableClient.GetEntityAsync<AdoConnectionEntity>("ado", id);
            return entity.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveAdoConnectionAsync(AdoConnectionEntity connection)
    {
        var tableClient = GetTableClient(AdoConnectionsTable);
        await tableClient.UpsertEntityAsync(connection);
    }

    public async Task DeleteAdoConnectionAsync(string id)
    {
        var tableClient = GetTableClient(AdoConnectionsTable);
        await tableClient.DeleteEntityAsync("ado", id);
    }

    // Pipelines
    public async Task<IEnumerable<PipelineEntity>> GetAllPipelinesAsync()
    {
        var tableClient = GetTableClient(PipelinesTable);
        var entities = new List<PipelineEntity>();
        await foreach (var entity in tableClient.QueryAsync<PipelineEntity>())
        {
            entities.Add(entity);
        }
        return entities;
    }

    public async Task<IEnumerable<PipelineEntity>> GetActivePipelinesAsync()
    {
        var tableClient = GetTableClient(PipelinesTable);
        var entities = new List<PipelineEntity>();
        await foreach (var entity in tableClient.QueryAsync<PipelineEntity>(e => e.IsActive))
        {
            entities.Add(entity);
        }
        return entities;
    }

    public async Task<PipelineEntity?> GetPipelineAsync(string orgId, string pipelineId)
    {
        var tableClient = GetTableClient(PipelinesTable);
        try
        {
            var entity = await tableClient.GetEntityAsync<PipelineEntity>(orgId, pipelineId);
            return entity.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<PipelineEntity?> GetPipelineByIdAsync(string pipelineId)
    {
        var tableClient = GetTableClient(PipelinesTable);
        await foreach (var entity in tableClient.QueryAsync<PipelineEntity>(e => e.PipelineId == pipelineId))
        {
            return entity;
        }
        return null;
    }

    public async Task SavePipelineAsync(PipelineEntity pipeline)
    {
        var tableClient = GetTableClient(PipelinesTable);
        await tableClient.UpsertEntityAsync(pipeline);
    }

    public async Task DeletePipelineAsync(string orgId, string pipelineId)
    {
        var tableClient = GetTableClient(PipelinesTable);
        await tableClient.DeleteEntityAsync(orgId, pipelineId);
    }

    // Drift Records
    public async Task<IEnumerable<DriftRecordEntity>> GetDriftRecordsAsync(
        DateTime? fromDate = null, DateTime? toDate = null, string? pipelineId = null, string? severity = null)
    {
        var tableClient = GetTableClient(DriftRecordsTable);
        var entities = new List<DriftRecordEntity>();

        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var partitionKey = date.ToString("yyyyMMdd");
            await foreach (var entity in tableClient.QueryAsync<DriftRecordEntity>(e => e.PartitionKey == partitionKey))
            {
                if (pipelineId != null && entity.PipelineId != pipelineId) continue;
                if (severity != null && entity.Severity != severity) continue;
                entities.Add(entity);
            }
        }

        return entities.OrderByDescending(e => e.DetectedAt);
    }

    public async Task<DriftRecordEntity?> GetDriftRecordAsync(string id)
    {
        var tableClient = GetTableClient(DriftRecordsTable);
        // Search across partitions
        await foreach (var entity in tableClient.QueryAsync<DriftRecordEntity>(e => e.RowKey == id))
        {
            return entity;
        }
        return null;
    }

    public async Task<IEnumerable<DriftRecordEntity>> GetDriftRecordsByPipelineAsync(string pipelineId)
    {
        var tableClient = GetTableClient(DriftRecordsTable);
        var entities = new List<DriftRecordEntity>();
        await foreach (var entity in tableClient.QueryAsync<DriftRecordEntity>(e => e.PipelineId == pipelineId))
        {
            entities.Add(entity);
        }
        return entities.OrderByDescending(e => e.DetectedAt);
    }

    public async Task SaveDriftRecordAsync(DriftRecordEntity record)
    {
        var tableClient = GetTableClient(DriftRecordsTable);
        record.PartitionKey = record.DetectedAt.ToString("yyyyMMdd");
        record.RowKey = string.IsNullOrEmpty(record.RowKey) ? Guid.NewGuid().ToString() : record.RowKey;
        await tableClient.UpsertEntityAsync(record);
    }

    public async Task DeleteOldDriftRecordsAsync(int retentionDays)
    {
        var tableClient = GetTableClient(DriftRecordsTable);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        for (var date = cutoffDate.AddDays(-365); date < cutoffDate; date = date.AddDays(1))
        {
            var partitionKey = date.ToString("yyyyMMdd");
            await foreach (var entity in tableClient.QueryAsync<DriftRecordEntity>(e => e.PartitionKey == partitionKey))
            {
                await tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            }
        }
    }

    public async Task<DriftStats> GetDriftStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var drifts = await GetDriftRecordsAsync(fromDate, toDate);
        var driftList = drifts.ToList();

        return new DriftStats
        {
            Total = driftList.Count,
            Critical = driftList.Count(d => d.Severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase)),
            High = driftList.Count(d => d.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase)),
            Medium = driftList.Count(d => d.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase)),
            Low = driftList.Count(d => d.Severity.Equals("LOW", StringComparison.OrdinalIgnoreCase)),
            LastAnalysis = driftList.OrderByDescending(d => d.DetectedAt).FirstOrDefault()?.DetectedAt
        };
    }

    // Scan Logs
    public async Task<IEnumerable<ScanLogEntity>> GetScanLogsAsync(
        DateTime? fromDate = null, DateTime? toDate = null, string? pipelineId = null, string? status = null)
    {
        var tableClient = GetTableClient(ScanLogsTable);
        var entities = new List<ScanLogEntity>();

        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var partitionKey = date.ToString("yyyyMMdd");
            await foreach (var entity in tableClient.QueryAsync<ScanLogEntity>(e => e.PartitionKey == partitionKey))
            {
                if (pipelineId != null && entity.PipelineId != pipelineId) continue;
                if (status != null && entity.Status != status) continue;
                entities.Add(entity);
            }
        }

        return entities.OrderByDescending(e => e.StartedAt);
    }

    public async Task<ScanLogEntity?> GetScanLogAsync(string id)
    {
        var tableClient = GetTableClient(ScanLogsTable);
        await foreach (var entity in tableClient.QueryAsync<ScanLogEntity>(e => e.RowKey == id))
        {
            return entity;
        }
        return null;
    }

    public async Task<IEnumerable<ScanLogEntity>> GetScanLogsByPipelineAsync(string pipelineId, int limit = 10)
    {
        var tableClient = GetTableClient(ScanLogsTable);
        var entities = new List<ScanLogEntity>();
        await foreach (var entity in tableClient.QueryAsync<ScanLogEntity>(e => e.PipelineId == pipelineId))
        {
            entities.Add(entity);
        }
        return entities.OrderByDescending(e => e.StartedAt).Take(limit);
    }

    public async Task SaveScanLogAsync(ScanLogEntity log)
    {
        var tableClient = GetTableClient(ScanLogsTable);
        log.PartitionKey = log.StartedAt.ToString("yyyyMMdd");
        log.RowKey = string.IsNullOrEmpty(log.RowKey) ? Guid.NewGuid().ToString() : log.RowKey;
        await tableClient.UpsertEntityAsync(log);
    }

    public async Task<ScanStats> GetScanStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var scans = await GetScanLogsAsync(fromDate, toDate);
        var scanList = scans.ToList();

        var completedScans = scanList.Where(s => s.Status == "success" || s.Status == "failed").ToList();

        return new ScanStats
        {
            Total = scanList.Count,
            Success = scanList.Count(s => s.Status == "success"),
            Failed = scanList.Count(s => s.Status == "failed"),
            AvgDurationSeconds = completedScans.Count > 0
                ? (int)completedScans.Average(s => s.DurationSeconds)
                : 0
        };
    }

    // Analysis Status
    public async Task<AnalysisStatusEntity?> GetAnalysisStatusAsync(string correlationId)
    {
        var tableClient = GetTableClient(AnalysisStatusTable);
        try
        {
            var entity = await tableClient.GetEntityAsync<AnalysisStatusEntity>("analysis", correlationId);
            return entity.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveAnalysisStatusAsync(AnalysisStatusEntity status)
    {
        var tableClient = GetTableClient(AnalysisStatusTable);
        status.PartitionKey = "analysis";
        status.RowKey = status.RowKey;
        await tableClient.UpsertEntityAsync(status);
    }

    // Email Recipients
    public async Task<IEnumerable<EmailRecipientEntity>> GetEmailRecipientsAsync(bool activeOnly = true)
    {
        var tableClient = GetTableClient(EmailRecipientsTable);
        var entities = new List<EmailRecipientEntity>();

        if (activeOnly)
        {
            await foreach (var entity in tableClient.QueryAsync<EmailRecipientEntity>(e => e.PartitionKey == "recipients" && e.IsActive))
            {
                entities.Add(entity);
            }
        }
        else
        {
            await foreach (var entity in tableClient.QueryAsync<EmailRecipientEntity>(e => e.PartitionKey == "recipients"))
            {
                entities.Add(entity);
            }
        }

        return entities;
    }

    public async Task SaveEmailRecipientAsync(EmailRecipientEntity recipient)
    {
        var tableClient = GetTableClient(EmailRecipientsTable);
        recipient.RowKey = string.IsNullOrEmpty(recipient.RowKey) ? Guid.NewGuid().ToString() : recipient.RowKey;
        await tableClient.UpsertEntityAsync(recipient);
    }

    public async Task DeleteEmailRecipientAsync(string id)
    {
        var tableClient = GetTableClient(EmailRecipientsTable);
        await tableClient.DeleteEntityAsync("recipients", id);
    }

    // AI Connection
    public async Task<AiConnectionEntity?> GetActiveAiConnectionAsync()
    {
        var tableClient = GetTableClient(AiConnectionsTable);
        await foreach (var entity in tableClient.QueryAsync<AiConnectionEntity>(e => e.PartitionKey == "ai" && e.Status == "active"))
        {
            return entity;
        }
        return null;
    }

    public async Task SaveAiConnectionAsync(AiConnectionEntity connection)
    {
        var tableClient = GetTableClient(AiConnectionsTable);
        connection.RowKey = string.IsNullOrEmpty(connection.RowKey) ? Guid.NewGuid().ToString() : connection.RowKey;
        await tableClient.UpsertEntityAsync(connection);
    }

    // Email Service Config
    public async Task<EmailServiceConfigEntity?> GetEmailServiceConfigAsync()
    {
        var tableClient = GetTableClient(EmailServiceConfigTable);
        try
        {
            var entity = await tableClient.GetEntityAsync<EmailServiceConfigEntity>("email", "primary");
            return entity.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveEmailServiceConfigAsync(EmailServiceConfigEntity config)
    {
        var tableClient = GetTableClient(EmailServiceConfigTable);
        await tableClient.UpsertEntityAsync(config);
    }
}
