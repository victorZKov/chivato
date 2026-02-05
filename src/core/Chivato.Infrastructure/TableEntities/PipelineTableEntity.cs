using Chivato.Domain.Entities;

namespace Chivato.Infrastructure.TableEntities;

public class PipelineTableEntity : BaseTableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string RepositoryId { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string TerraformPath { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset? LastScanAt { get; set; }
    public string? LastScanStatus { get; set; }
    public string? LastScanError { get; set; }
    public int DriftCount { get; set; }
    public string? LastScanCorrelationId { get; set; }

    // Connection references (new model)
    public string? AdoConnectionId { get; set; }
    public string? AzureConnectionId { get; set; }
    public string? PipelineId { get; set; }  // ADO Pipeline ID

    // IaC drift detection settings
    public string? RepositoryName { get; set; }
    public string PlanOnlyParameter { get; set; } = "PLAN_ONLY";

    public static PipelineTableEntity FromDomain(Pipeline pipeline)
    {
        return new PipelineTableEntity
        {
            PartitionKey = pipeline.TenantId,
            RowKey = pipeline.Id,
            Name = pipeline.Name,
            Organization = pipeline.Organization,
            Project = pipeline.Project,
            RepositoryId = pipeline.RepositoryId,
            Branch = pipeline.Branch,
            TerraformPath = pipeline.TerraformPath,
            SubscriptionId = pipeline.SubscriptionId,
            ResourceGroup = pipeline.ResourceGroup,
            RepositoryName = pipeline.RepositoryName,
            PlanOnlyParameter = pipeline.PlanOnlyParameter,
            Status = pipeline.Status.ToString(),
            LastScanAt = pipeline.LastScanAt,
            LastScanStatus = pipeline.LastScanStatus,
            LastScanError = pipeline.LastScanError,
            DriftCount = pipeline.DriftCount,
            LastScanCorrelationId = pipeline.LastScanCorrelationId,
            AdoConnectionId = pipeline.AdoConnectionId,
            AzureConnectionId = pipeline.AzureConnectionId,
            PipelineId = pipeline.PipelineId,
            CreatedAt = pipeline.CreatedAt,
            UpdatedAt = pipeline.UpdatedAt
        };
    }

    public Pipeline ToDomain()
    {
        return Pipeline.Reconstitute(
            id: RowKey,
            tenantId: PartitionKey,
            name: Name,
            organization: Organization,
            project: Project,
            repositoryId: RepositoryId,
            branch: Branch,
            terraformPath: TerraformPath,
            subscriptionId: SubscriptionId,
            resourceGroup: ResourceGroup,
            status: Enum.Parse<PipelineStatus>(Status),
            lastScanAt: LastScanAt,
            lastScanStatus: LastScanStatus,
            lastScanError: LastScanError,
            driftCount: DriftCount,
            lastScanCorrelationId: LastScanCorrelationId,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt,
            adoConnectionId: AdoConnectionId,
            azureConnectionId: AzureConnectionId,
            pipelineId: PipelineId,
            repositoryName: RepositoryName,
            planOnlyParameter: PlanOnlyParameter
        );
    }
}
