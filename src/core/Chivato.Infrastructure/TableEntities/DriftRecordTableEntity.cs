using Chivato.Domain.Entities;
using Chivato.Domain.ValueObjects;

namespace Chivato.Infrastructure.TableEntities;

public class DriftRecordTableEntity : BaseTableEntity
{
    public string PipelineId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Severity { get; set; } = "Low";
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset DetectedAt { get; set; }
    public string Status { get; set; } = "Open";
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }

    public static DriftRecordTableEntity FromDomain(DriftRecord drift)
    {
        return new DriftRecordTableEntity
        {
            PartitionKey = drift.TenantId,
            RowKey = drift.Id,
            PipelineId = drift.PipelineId,
            CorrelationId = drift.CorrelationId,
            Severity = drift.Severity.ToString(),
            ResourceId = drift.ResourceId,
            ResourceType = drift.ResourceType,
            ResourceName = drift.ResourceName,
            Property = drift.Property,
            ExpectedValue = drift.ExpectedValue,
            ActualValue = drift.ActualValue,
            Description = drift.Description,
            Recommendation = drift.Recommendation,
            Category = drift.Category,
            DetectedAt = drift.DetectedAt,
            Status = drift.Status.ToString(),
            ResolvedAt = drift.ResolvedAt,
            ResolvedBy = drift.ResolvedBy,
            CreatedAt = drift.CreatedAt,
            UpdatedAt = drift.UpdatedAt
        };
    }

    public DriftRecord ToDomain()
    {
        return DriftRecord.Reconstitute(
            id: RowKey,
            tenantId: PartitionKey,
            pipelineId: PipelineId,
            correlationId: CorrelationId,
            severity: Enum.Parse<Severity>(Severity),
            resourceId: ResourceId,
            resourceType: ResourceType,
            resourceName: ResourceName,
            property: Property,
            expectedValue: ExpectedValue,
            actualValue: ActualValue,
            description: Description,
            recommendation: Recommendation,
            category: Category,
            detectedAt: DetectedAt,
            status: Enum.Parse<DriftStatus>(Status),
            resolvedAt: ResolvedAt,
            resolvedBy: ResolvedBy,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
