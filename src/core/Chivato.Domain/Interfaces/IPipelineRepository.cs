using Chivato.Domain.Entities;

namespace Chivato.Domain.Interfaces;

public interface IPipelineRepository
{
    Task<Pipeline?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<IReadOnlyList<Pipeline>> GetAllAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Pipeline>> GetActiveAsync(string tenantId, CancellationToken ct = default);
    Task AddAsync(Pipeline pipeline, CancellationToken ct = default);
    Task UpdateAsync(Pipeline pipeline, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken ct = default);
}

public interface IDriftRecordRepository
{
    Task<DriftRecord?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<PagedResult<DriftRecord>> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? severity = null,
        string? pipelineId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);
    Task<DriftStats> GetStatsAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DriftRecord>> GetByCorrelationIdAsync(string tenantId, string correlationId, CancellationToken ct = default);
    Task AddAsync(DriftRecord drift, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<DriftRecord> drifts, CancellationToken ct = default);
    Task UpdateAsync(DriftRecord drift, CancellationToken ct = default);
}

public interface IScanLogRepository
{
    Task<ScanLog?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<ScanLog?> GetByCorrelationIdAsync(string tenantId, string correlationId, CancellationToken ct = default);
    Task<PagedResult<ScanLog>> GetPagedAsync(
        string tenantId,
        int page,
        int pageSize,
        string? status = null,
        string? pipelineId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);
    Task<ScanStats> GetStatsAsync(string tenantId, CancellationToken ct = default);
    Task AddAsync(ScanLog scanLog, CancellationToken ct = default);
    Task UpdateAsync(ScanLog scanLog, CancellationToken ct = default);
}

// Common types
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record DriftStats(int Total, int Critical, int High, int Medium, int Low, DateTimeOffset? LastAnalysis);

public record ScanStats(int Total, int Success, int Failed, double AvgDurationSeconds);
