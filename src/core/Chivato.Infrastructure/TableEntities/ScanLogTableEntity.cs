using Chivato.Domain.Entities;

namespace Chivato.Infrastructure.TableEntities;

public class ScanLogTableEntity : BaseTableEntity
{
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int DriftCount { get; set; }
    public int ResourcesScanned { get; set; }
    public int DurationSeconds { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? OverallRisk { get; set; }

    public static ScanLogTableEntity FromDomain(ScanLog scanLog)
    {
        return new ScanLogTableEntity
        {
            PartitionKey = scanLog.TenantId,
            RowKey = scanLog.Id,
            PipelineId = scanLog.PipelineId,
            PipelineName = scanLog.PipelineName,
            CorrelationId = scanLog.CorrelationId,
            Status = scanLog.Status.ToString(),
            StartedAt = scanLog.StartedAt,
            CompletedAt = scanLog.CompletedAt,
            DriftCount = scanLog.DriftCount,
            ResourcesScanned = scanLog.ResourcesScanned,
            DurationSeconds = scanLog.DurationSeconds,
            TriggeredBy = scanLog.TriggeredBy,
            ErrorMessage = scanLog.ErrorMessage,
            OverallRisk = scanLog.OverallRisk,
            CreatedAt = scanLog.CreatedAt,
            UpdatedAt = scanLog.UpdatedAt
        };
    }

    public ScanLog ToDomain()
    {
        return ScanLog.Reconstitute(
            id: RowKey,
            tenantId: PartitionKey,
            pipelineId: PipelineId,
            pipelineName: PipelineName,
            correlationId: CorrelationId,
            status: Enum.Parse<ScanStatus>(Status),
            startedAt: StartedAt,
            completedAt: CompletedAt,
            driftCount: DriftCount,
            resourcesScanned: ResourcesScanned,
            durationSeconds: DurationSeconds,
            triggeredBy: TriggeredBy,
            errorMessage: ErrorMessage,
            overallRisk: OverallRisk,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
