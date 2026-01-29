using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Entities;

/// <summary>
/// Represents a detected infrastructure drift
/// </summary>
public class DriftRecord : BaseEntity
{
    public string PipelineId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public Severity Severity { get; private set; }
    public string ResourceId { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceName { get; private set; } = string.Empty;
    public string Property { get; private set; } = string.Empty;
    public string ExpectedValue { get; private set; } = string.Empty;
    public string ActualValue { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Recommendation { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public DateTimeOffset DetectedAt { get; private set; }
    public DriftStatus Status { get; private set; } = DriftStatus.Open;
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }

    private DriftRecord() { }

    public static DriftRecord Create(
        string tenantId,
        string pipelineId,
        string correlationId,
        Severity severity,
        string resourceId,
        string resourceType,
        string resourceName,
        string property,
        string expectedValue,
        string actualValue,
        string description,
        string recommendation,
        string category)
    {
        return new DriftRecord
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            PipelineId = pipelineId,
            CorrelationId = correlationId,
            Severity = severity,
            ResourceId = resourceId,
            ResourceType = resourceType,
            ResourceName = resourceName,
            Property = property,
            ExpectedValue = expectedValue,
            ActualValue = actualValue,
            Description = description,
            Recommendation = recommendation,
            Category = category,
            DetectedAt = DateTimeOffset.UtcNow,
            Status = DriftStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Resolve(string userId)
    {
        Status = DriftStatus.Resolved;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedBy = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Ignore(string userId)
    {
        Status = DriftStatus.Ignored;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedBy = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum DriftStatus
{
    Open,
    Resolved,
    Ignored
}
