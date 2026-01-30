using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Entities;

/// <summary>
/// Credential stored in Key Vault with expiration tracking
/// </summary>
public class Credential : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public CredentialType Type { get; private set; }
    public string KeyVaultSecretName { get; private set; } = string.Empty;
    public CredentialStatus Status { get; private set; } = CredentialStatus.Unknown;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastRotatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public string? AssociatedResourceId { get; private set; }

    private Credential() { }

    public static Credential Create(
        string tenantId,
        string name,
        CredentialType type,
        string keyVaultSecretName,
        DateTimeOffset? expiresAt = null,
        string? associatedResourceId = null)
    {
        var credential = new Credential
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = name,
            Type = type,
            KeyVaultSecretName = keyVaultSecretName,
            Status = CredentialStatus.Active,
            ExpiresAt = expiresAt,
            AssociatedResourceId = associatedResourceId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        credential.AddDomainEvent(new CredentialCreatedEvent(credential.Id, credential.TenantId, credential.Type));

        return credential;
    }

    public void MarkAsUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
    }

    public void Rotate(DateTimeOffset? newExpiresAt = null)
    {
        LastRotatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = newExpiresAt;
        Status = CredentialStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new CredentialRotatedEvent(Id, TenantId, Type));
    }

    public void MarkAsExpired()
    {
        Status = CredentialStatus.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new CredentialExpiredEvent(Id, TenantId, Type, Name));
    }

    public void MarkAsInvalid(string reason)
    {
        Status = CredentialStatus.Invalid;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new CredentialInvalidEvent(Id, TenantId, Type, reason));
    }

    public bool IsExpiringSoon(int daysThreshold = 7)
    {
        if (!ExpiresAt.HasValue) return false;
        return ExpiresAt.Value <= DateTimeOffset.UtcNow.AddDays(daysThreshold);
    }

    public bool IsExpired()
    {
        if (!ExpiresAt.HasValue) return false;
        return ExpiresAt.Value <= DateTimeOffset.UtcNow;
    }

    public int? DaysUntilExpiration()
    {
        if (!ExpiresAt.HasValue) return null;
        var days = (ExpiresAt.Value - DateTimeOffset.UtcNow).Days;
        return days < 0 ? 0 : days;
    }
}

public enum CredentialType
{
    AzureServicePrincipal,
    AdoPatToken,
    AiApiKey,
    WebhookSecret,
    Other
}

public enum CredentialStatus
{
    Unknown,
    Active,
    Expiring,
    Expired,
    Invalid
}

// Domain Events
public record CredentialCreatedEvent(string CredentialId, string TenantId, CredentialType Type) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record CredentialRotatedEvent(string CredentialId, string TenantId, CredentialType Type) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record CredentialExpiredEvent(string CredentialId, string TenantId, CredentialType Type, string Name) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record CredentialInvalidEvent(string CredentialId, string TenantId, CredentialType Type, string Reason) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
