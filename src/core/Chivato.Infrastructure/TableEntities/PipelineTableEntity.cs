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
    public int DriftCount { get; set; }
    public string? LastScanCorrelationId { get; set; }

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
            Status = pipeline.Status.ToString(),
            LastScanAt = pipeline.LastScanAt,
            DriftCount = pipeline.DriftCount,
            LastScanCorrelationId = pipeline.LastScanCorrelationId,
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
            driftCount: DriftCount,
            lastScanCorrelationId: LastScanCorrelationId,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
