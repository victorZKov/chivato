using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Entities;

/// <summary>
/// Represents an Azure DevOps pipeline being monitored for drift
/// </summary>
public class Pipeline : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Organization { get; private set; } = string.Empty;
    public string Project { get; private set; } = string.Empty;
    public string RepositoryId { get; private set; } = string.Empty;
    public string Branch { get; private set; } = "main";
    public string TerraformPath { get; private set; } = string.Empty;
    public string SubscriptionId { get; private set; } = string.Empty;
    public string ResourceGroup { get; private set; } = string.Empty;
    public PipelineStatus Status { get; private set; } = PipelineStatus.Active;
    public DateTimeOffset? LastScanAt { get; private set; }
    public int DriftCount { get; private set; }
    public string? LastScanCorrelationId { get; private set; }

    private Pipeline() { } // EF/ORM

    public static Pipeline Create(
        string tenantId,
        string name,
        string organization,
        string project,
        string repositoryId,
        string branch,
        string terraformPath,
        string subscriptionId,
        string resourceGroup)
    {
        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = name,
            Organization = organization,
            Project = project,
            RepositoryId = repositoryId,
            Branch = branch,
            TerraformPath = terraformPath,
            SubscriptionId = subscriptionId,
            ResourceGroup = resourceGroup,
            Status = PipelineStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        pipeline.AddDomainEvent(new PipelineCreatedEvent(pipeline.Id, pipeline.TenantId, pipeline.Name));

        return pipeline;
    }

    public void Update(
        string name,
        string branch,
        string terraformPath,
        string subscriptionId,
        string resourceGroup)
    {
        Name = name;
        Branch = branch;
        TerraformPath = terraformPath;
        SubscriptionId = subscriptionId;
        ResourceGroup = resourceGroup;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordScan(string correlationId, int driftCount)
    {
        LastScanAt = DateTimeOffset.UtcNow;
        LastScanCorrelationId = correlationId;
        DriftCount = driftCount;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PipelineScanCompletedEvent(Id, TenantId, correlationId, driftCount));
    }

    public void Activate()
    {
        Status = PipelineStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        Status = PipelineStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reconstitute a Pipeline from persistence (no domain events raised)
    /// </summary>
    public static Pipeline Reconstitute(
        string id,
        string tenantId,
        string name,
        string organization,
        string project,
        string repositoryId,
        string branch,
        string terraformPath,
        string subscriptionId,
        string resourceGroup,
        PipelineStatus status,
        DateTimeOffset? lastScanAt,
        int driftCount,
        string? lastScanCorrelationId,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        return new Pipeline
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Organization = organization,
            Project = project,
            RepositoryId = repositoryId,
            Branch = branch,
            TerraformPath = terraformPath,
            SubscriptionId = subscriptionId,
            ResourceGroup = resourceGroup,
            Status = status,
            LastScanAt = lastScanAt,
            DriftCount = driftCount,
            LastScanCorrelationId = lastScanCorrelationId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

public enum PipelineStatus
{
    Active,
    Inactive,
    Error
}

// Domain Events
public record PipelineCreatedEvent(string PipelineId, string TenantId, string Name) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record PipelineScanCompletedEvent(string PipelineId, string TenantId, string CorrelationId, int DriftCount) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
