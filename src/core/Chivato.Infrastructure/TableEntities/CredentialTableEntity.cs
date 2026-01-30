using Chivato.Domain.Entities;

namespace Chivato.Infrastructure.TableEntities;

public class CredentialTableEntity : BaseTableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Other";
    public string KeyVaultSecretName { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastRotatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public string? AssociatedResourceId { get; set; }

    public static CredentialTableEntity FromDomain(Credential credential)
    {
        return new CredentialTableEntity
        {
            PartitionKey = credential.TenantId,
            RowKey = credential.Id,
            Name = credential.Name,
            Type = credential.Type.ToString(),
            KeyVaultSecretName = credential.KeyVaultSecretName,
            Status = credential.Status.ToString(),
            ExpiresAt = credential.ExpiresAt,
            LastRotatedAt = credential.LastRotatedAt,
            LastUsedAt = credential.LastUsedAt,
            AssociatedResourceId = credential.AssociatedResourceId,
            CreatedAt = credential.CreatedAt,
            UpdatedAt = credential.UpdatedAt
        };
    }

    public Credential ToDomain()
    {
        return Credential.Reconstitute(
            id: RowKey,
            tenantId: PartitionKey,
            name: Name,
            type: Enum.Parse<CredentialType>(Type),
            keyVaultSecretName: KeyVaultSecretName,
            status: Enum.Parse<CredentialStatus>(Status),
            expiresAt: ExpiresAt,
            lastRotatedAt: LastRotatedAt,
            lastUsedAt: LastUsedAt,
            associatedResourceId: AssociatedResourceId,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
