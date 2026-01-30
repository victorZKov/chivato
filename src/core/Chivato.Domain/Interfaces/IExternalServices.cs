namespace Chivato.Domain.Interfaces;

/// <summary>
/// Azure Key Vault service for secret management
/// </summary>
public interface IKeyVaultService
{
    Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default);
    Task SetSecretAsync(string secretName, string value, DateTimeOffset? expiresOn = null, CancellationToken ct = default);
    Task DeleteSecretAsync(string secretName, CancellationToken ct = default);
    Task<DateTimeOffset?> GetSecretExpirationAsync(string secretName, CancellationToken ct = default);
}

/// <summary>
/// Azure Resource Manager service for reading resource state
/// </summary>
public interface IAzureResourceService
{
    Task<IReadOnlyList<AzureResource>> GetResourcesInGroupAsync(
        string subscriptionId,
        string resourceGroup,
        CancellationToken ct = default);

    Task<AzureResource?> GetResourceAsync(
        string resourceId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, object>> GetResourcePropertiesAsync(
        string resourceId,
        CancellationToken ct = default);
}

/// <summary>
/// Azure DevOps service for reading pipeline/repo info
/// </summary>
public interface IAdoService
{
    Task<string?> GetFileContentAsync(
        string organization,
        string project,
        string repositoryId,
        string filePath,
        string branch,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListFilesAsync(
        string organization,
        string project,
        string repositoryId,
        string path,
        string branch,
        CancellationToken ct = default);

    Task<bool> TestConnectionAsync(
        string organization,
        CancellationToken ct = default);
}

/// <summary>
/// SignalR service for real-time notifications
/// </summary>
public interface ISignalRService
{
    Task SendToTenantAsync(string tenantId, string eventName, object payload, CancellationToken ct = default);
    Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken ct = default);
    Task AddUserToTenantGroupAsync(string userId, string tenantId, CancellationToken ct = default);
    Task RemoveUserFromTenantGroupAsync(string userId, string tenantId, CancellationToken ct = default);
}

/// <summary>
/// Email service for notifications
/// </summary>
public interface IEmailService
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default);

    Task SendBulkAsync(
        IEnumerable<string> recipients,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}

/// <summary>
/// Message queue service for async processing
/// </summary>
public interface IMessageQueueService
{
    Task SendAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class;
    Task SendAsync<T>(string queueName, T message, TimeSpan delay, CancellationToken ct = default) where T : class;
}

// Common types for external services
public record AzureResource(
    string Id,
    string Name,
    string Type,
    string ResourceGroup,
    string Location,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<string, object> Properties
);
