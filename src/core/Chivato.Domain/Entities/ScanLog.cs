namespace Chivato.Domain.Entities;

/// <summary>
/// Represents a scan execution log
/// </summary>
public class ScanLog : BaseEntity
{
    public string PipelineId { get; private set; } = string.Empty;
    public string PipelineName { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public ScanStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int DriftCount { get; private set; }
    public int ResourcesScanned { get; private set; }
    public int DurationSeconds { get; private set; }
    public string TriggeredBy { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public string? OverallRisk { get; private set; }

    private ScanLog() { }

    public static ScanLog Start(
        string tenantId,
        string pipelineId,
        string pipelineName,
        string correlationId,
        string triggeredBy)
    {
        return new ScanLog
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            PipelineId = pipelineId,
            PipelineName = pipelineName,
            CorrelationId = correlationId,
            Status = ScanStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            TriggeredBy = triggeredBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Complete(int driftCount, int resourcesScanned, string? overallRisk)
    {
        Status = ScanStatus.Success;
        CompletedAt = DateTimeOffset.UtcNow;
        DriftCount = driftCount;
        ResourcesScanned = resourcesScanned;
        OverallRisk = overallRisk;
        DurationSeconds = (int)(CompletedAt.Value - StartedAt).TotalSeconds;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = ScanStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        ErrorMessage = errorMessage;
        DurationSeconds = (int)(CompletedAt.Value - StartedAt).TotalSeconds;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reconstitute a ScanLog from persistence
    /// </summary>
    public static ScanLog Reconstitute(
        string id,
        string tenantId,
        string pipelineId,
        string pipelineName,
        string correlationId,
        ScanStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        int driftCount,
        int resourcesScanned,
        int durationSeconds,
        string triggeredBy,
        string? errorMessage,
        string? overallRisk,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        return new ScanLog
        {
            Id = id,
            TenantId = tenantId,
            PipelineId = pipelineId,
            PipelineName = pipelineName,
            CorrelationId = correlationId,
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DriftCount = driftCount,
            ResourcesScanned = resourcesScanned,
            DurationSeconds = durationSeconds,
            TriggeredBy = triggeredBy,
            ErrorMessage = errorMessage,
            OverallRisk = overallRisk,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

public enum ScanStatus
{
    Pending,
    Running,
    Success,
    Failed
}
