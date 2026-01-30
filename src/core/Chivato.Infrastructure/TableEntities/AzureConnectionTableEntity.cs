using Chivato.Domain.Entities;
using Chivato.Domain.ValueObjects;

namespace Chivato.Infrastructure.TableEntities;

public class AzureConnectionTableEntity : BaseTableEntity
{
    public string Name { get; set; } = string.Empty;
    public string AzureTenantId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecretKeyVaultKey { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public DateTimeOffset? LastTestedAt { get; set; }
    public string? LastTestError { get; set; }
    public bool IsDefault { get; set; }

    public static AzureConnectionTableEntity FromDomain(AzureConnection connection)
    {
        return new AzureConnectionTableEntity
        {
            PartitionKey = connection.TenantId,
            RowKey = connection.Id,
            Name = connection.Name,
            AzureTenantId = connection.AzureTenantId,
            SubscriptionId = connection.SubscriptionId,
            ClientId = connection.ClientId,
            ClientSecretKeyVaultKey = connection.ClientSecretKeyVaultKey,
            Status = connection.Status.ToString(),
            LastTestedAt = connection.LastTestedAt,
            LastTestError = connection.LastTestError,
            IsDefault = connection.IsDefault,
            CreatedAt = connection.CreatedAt,
            UpdatedAt = connection.UpdatedAt
        };
    }

    public AzureConnection ToDomain()
    {
        return AzureConnection.Reconstitute(
            id: RowKey,
            tenantId: PartitionKey,
            name: Name,
            azureTenantId: AzureTenantId,
            subscriptionId: SubscriptionId,
            clientId: ClientId,
            clientSecretKeyVaultKey: ClientSecretKeyVaultKey,
            status: Enum.Parse<ConnectionStatus>(Status),
            lastTestedAt: LastTestedAt,
            lastTestError: LastTestError,
            isDefault: IsDefault,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
