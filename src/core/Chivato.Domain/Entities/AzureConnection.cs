using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Entities;

/// <summary>
/// Azure subscription connection for resource scanning
/// </summary>
public class AzureConnection : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string SubscriptionId { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public string ClientSecretKeyVaultKey { get; private set; } = string.Empty;
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Unknown;
    public DateTimeOffset? LastTestedAt { get; private set; }
    public string? LastTestError { get; private set; }
    public bool IsDefault { get; private set; }

    private AzureConnection() { }

    public static AzureConnection Create(
        string tenantId,
        string name,
        string subscriptionId,
        string clientId,
        string clientSecretKeyVaultKey,
        bool isDefault = false)
    {
        var connection = new AzureConnection
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = name,
            SubscriptionId = subscriptionId,
            ClientId = clientId,
            ClientSecretKeyVaultKey = clientSecretKeyVaultKey,
            Status = ConnectionStatus.Unknown,
            IsDefault = isDefault,
            CreatedAt = DateTimeOffset.UtcNow
        };

        connection.AddDomainEvent(new AzureConnectionCreatedEvent(connection.Id, connection.TenantId, connection.Name));

        return connection;
    }

    public void Update(string name, string subscriptionId, string clientId, string clientSecretKeyVaultKey)
    {
        Name = name;
        SubscriptionId = subscriptionId;
        ClientId = clientId;
        ClientSecretKeyVaultKey = clientSecretKeyVaultKey;
        Status = ConnectionStatus.Unknown; // Reset status after update
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsDefault()
    {
        IsDefault = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UnmarkAsDefault()
    {
        IsDefault = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordTestSuccess()
    {
        Status = ConnectionStatus.Connected;
        LastTestedAt = DateTimeOffset.UtcNow;
        LastTestError = null;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ConnectionTestedEvent(Id, TenantId, "Azure", true, null));
    }

    public void RecordTestFailure(string error)
    {
        Status = ConnectionStatus.Error;
        LastTestedAt = DateTimeOffset.UtcNow;
        LastTestError = error;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ConnectionTestedEvent(Id, TenantId, "Azure", false, error));
    }
}

// Domain Events
public record AzureConnectionCreatedEvent(string ConnectionId, string TenantId, string Name) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record ConnectionTestedEvent(string ConnectionId, string TenantId, string ConnectionType, bool Success, string? Error) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
