using Chivato.Shared.Models;

namespace Chivato.Shared.Services;

public interface IStorageService
{
    // Configurations
    Task<string?> GetConfigValueAsync(string key);
    Task SetConfigValueAsync(string key, string value, string type);

    // Azure Connections
    Task<IEnumerable<AzureConnectionEntity>> GetAzureConnectionsAsync();
    Task<AzureConnectionEntity?> GetAzureConnectionAsync(string id);
    Task SaveAzureConnectionAsync(AzureConnectionEntity connection);
    Task DeleteAzureConnectionAsync(string id);

    // ADO Connections
    Task<IEnumerable<AdoConnectionEntity>> GetAdoConnectionsAsync();
    Task<AdoConnectionEntity?> GetAdoConnectionAsync(string id);
    Task SaveAdoConnectionAsync(AdoConnectionEntity connection);
    Task DeleteAdoConnectionAsync(string id);

    // Pipelines
    Task<IEnumerable<PipelineEntity>> GetAllPipelinesAsync();
    Task<IEnumerable<PipelineEntity>> GetActivePipelinesAsync();
    Task<PipelineEntity?> GetPipelineAsync(string orgId, string pipelineId);
    Task<PipelineEntity?> GetPipelineByIdAsync(string pipelineId);
    Task SavePipelineAsync(PipelineEntity pipeline);
    Task DeletePipelineAsync(string orgId, string pipelineId);

    // Drift Records
    Task<IEnumerable<DriftRecordEntity>> GetDriftRecordsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? pipelineId = null, string? severity = null);
    Task<DriftRecordEntity?> GetDriftRecordAsync(string id);
    Task<IEnumerable<DriftRecordEntity>> GetDriftRecordsByPipelineAsync(string pipelineId);
    Task SaveDriftRecordAsync(DriftRecordEntity record);
    Task DeleteOldDriftRecordsAsync(int retentionDays);
    Task<DriftStats> GetDriftStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);

    // Scan Logs
    Task<IEnumerable<ScanLogEntity>> GetScanLogsAsync(DateTime? fromDate = null, DateTime? toDate = null, string? pipelineId = null, string? status = null);
    Task<ScanLogEntity?> GetScanLogAsync(string id);
    Task<IEnumerable<ScanLogEntity>> GetScanLogsByPipelineAsync(string pipelineId, int limit = 10);
    Task SaveScanLogAsync(ScanLogEntity log);
    Task<ScanStats> GetScanStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);

    // Analysis Status (for tracking ad-hoc analysis)
    Task<AnalysisStatusEntity?> GetAnalysisStatusAsync(string correlationId);
    Task SaveAnalysisStatusAsync(AnalysisStatusEntity status);

    // Email Recipients
    Task<IEnumerable<EmailRecipientEntity>> GetEmailRecipientsAsync(bool activeOnly = true);
    Task SaveEmailRecipientAsync(EmailRecipientEntity recipient);
    Task DeleteEmailRecipientAsync(string id);

    // AI Connection
    Task<AiConnectionEntity?> GetActiveAiConnectionAsync();
    Task SaveAiConnectionAsync(AiConnectionEntity connection);

    // Email Service Config
    Task<EmailServiceConfigEntity?> GetEmailServiceConfigAsync();
    Task SaveEmailServiceConfigAsync(EmailServiceConfigEntity config);
}
