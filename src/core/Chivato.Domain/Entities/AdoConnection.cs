using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Entities;

/// <summary>
/// Azure DevOps organization connection
/// </summary>
public class AdoConnection : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Organization { get; private set; } = string.Empty;
    public string Project { get; private set; } = string.Empty;
    public string PatKeyVaultKey { get; private set; } = string.Empty;
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Unknown;
    public DateTimeOffset? LastTestedAt { get; private set; }
    public string? LastTestError { get; private set; }
    public bool IsDefault { get; private set; }

    private AdoConnection() { }

    public static AdoConnection Create(
        string tenantId,
        string name,
        string organization,
        string project,
        string patKeyVaultKey,
        bool isDefault = false)
    {
        var connection = new AdoConnection
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = name,
            Organization = organization,
            Project = project,
            PatKeyVaultKey = patKeyVaultKey,
            Status = ConnectionStatus.Unknown,
            IsDefault = isDefault,
            CreatedAt = DateTimeOffset.UtcNow
        };

        connection.AddDomainEvent(new AdoConnectionCreatedEvent(connection.Id, connection.TenantId, connection.Organization));

        return connection;
    }

    public void Update(string name, string organization, string project, string patKeyVaultKey)
    {
        Name = name;
        Organization = organization;
        Project = project;
        PatKeyVaultKey = patKeyVaultKey;
        Status = ConnectionStatus.Unknown;
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

        AddDomainEvent(new ConnectionTestedEvent(Id, TenantId, "ADO", true, null));
    }

    public void RecordTestFailure(string error)
    {
        Status = ConnectionStatus.Error;
        LastTestedAt = DateTimeOffset.UtcNow;
        LastTestError = error;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ConnectionTestedEvent(Id, TenantId, "ADO", false, error));
    }

    public string GetOrganizationUrl() => $"https://dev.azure.com/{Organization}";

    public string GetProjectUrl() => $"https://dev.azure.com/{Organization}/{Project}";
}

// Domain Events
public record AdoConnectionCreatedEvent(string ConnectionId, string TenantId, string Organization) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
