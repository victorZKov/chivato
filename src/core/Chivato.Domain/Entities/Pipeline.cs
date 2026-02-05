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
    public string? RepositoryName { get; private set; }
    public string PlanOnlyParameter { get; private set; } = "PLAN_ONLY";
    public PipelineStatus Status { get; private set; } = PipelineStatus.Active;
    public DateTimeOffset? LastScanAt { get; private set; }
    public string? LastScanStatus { get; private set; }
    public string? LastScanError { get; private set; }
    public int DriftCount { get; private set; }
    public string? LastScanCorrelationId { get; private set; }

    // Connection references (new model)
    public string? AdoConnectionId { get; private set; }
    public string? AzureConnectionId { get; private set; }
    public string? PipelineId { get; private set; }  // ADO Pipeline ID

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

    /// <summary>
    /// Creates a Pipeline from connection references (new simplified model)
    /// </summary>
    public static Pipeline CreateFromConnections(
        string tenantId,
        string adoConnectionId,
        string azureConnectionId,
        string organization,
        string project,
        string pipelineId,
        string pipelineName,
        string subscriptionId)
    {
        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = pipelineName,
            Organization = organization,
            Project = project,
            PipelineId = pipelineId,
            AdoConnectionId = adoConnectionId,
            AzureConnectionId = azureConnectionId,
            SubscriptionId = subscriptionId,
            RepositoryId = string.Empty,  // Not needed in new model
            Branch = "main",
            TerraformPath = string.Empty, // Not needed in new model
            ResourceGroup = string.Empty, // Will be scanned dynamically
            Status = PipelineStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        pipeline.AddDomainEvent(new PipelineCreatedEvent(pipeline.Id, pipeline.TenantId, pipeline.Name));

        return pipeline;
    }

    public void Update(
        string? name = null,
        string? branch = null,
        string? terraformPath = null,
        string? subscriptionId = null,
        string? resourceGroup = null,
        string? repositoryName = null,
        string? planOnlyParameter = null,
        string? adoConnectionId = null)
    {
        if (name != null)
            Name = name;

        if (branch != null)
            Branch = branch;

        if (terraformPath != null)
            TerraformPath = terraformPath;

        if (subscriptionId != null)
            SubscriptionId = subscriptionId;

        if (resourceGroup != null)
            ResourceGroup = resourceGroup;

        if (repositoryName != null)
            RepositoryName = repositoryName;

        if (planOnlyParameter != null)
            PlanOnlyParameter = planOnlyParameter;

        if (adoConnectionId != null)
            AdoConnectionId = adoConnectionId;

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
        string? lastScanStatus,
        string? lastScanError,
        int driftCount,
        string? lastScanCorrelationId,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        string? adoConnectionId = null,
        string? azureConnectionId = null,
        string? pipelineId = null,
        string? repositoryName = null,
        string? planOnlyParameter = null)
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
            RepositoryName = repositoryName,
            PlanOnlyParameter = planOnlyParameter ?? "PLAN_ONLY",
            Status = status,
            LastScanAt = lastScanAt,
            LastScanStatus = lastScanStatus,
            LastScanError = lastScanError,
            DriftCount = driftCount,
            LastScanCorrelationId = lastScanCorrelationId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            AdoConnectionId = adoConnectionId,
            AzureConnectionId = azureConnectionId,
            PipelineId = pipelineId
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
