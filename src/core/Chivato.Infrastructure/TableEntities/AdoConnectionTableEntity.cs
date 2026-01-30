using Chivato.Domain.Entities;
using Chivato.Domain.ValueObjects;

namespace Chivato.Infrastructure.TableEntities;

public class AdoConnectionTableEntity : BaseTableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string PatKeyVaultKey { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public DateTimeOffset? LastTestedAt { get; set; }
    public string? LastTestError { get; set; }
    public bool IsDefault { get; set; }

    public static AdoConnectionTableEntity FromDomain(AdoConnection connection)
    {
        return new AdoConnectionTableEntity
        {
            PartitionKey = connection.TenantId,
            RowKey = connection.Id,
            Name = connection.Name,
            Organization = connection.Organization,
            Project = connection.Project,
            PatKeyVaultKey = connection.PatKeyVaultKey,
            Status = connection.Status.ToString(),
            LastTestedAt = connection.LastTestedAt,
            LastTestError = connection.LastTestError,
            IsDefault = connection.IsDefault,
            CreatedAt = connection.CreatedAt,
            UpdatedAt = connection.UpdatedAt
        };
    }

    public AdoConnection ToDomain()
    {
        return AdoConnection.Reconstitute(
            id: RowKey,
            tenantId: PartitionKey,
            name: Name,
            organization: Organization,
            project: Project,
            patKeyVaultKey: PatKeyVaultKey,
            status: Enum.Parse<ConnectionStatus>(Status),
            lastTestedAt: LastTestedAt,
            lastTestError: LastTestError,
            isDefault: IsDefault,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
