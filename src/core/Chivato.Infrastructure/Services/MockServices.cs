using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chivato.Infrastructure.Services;

/// <summary>
/// Mock Key Vault service for development without Azure Key Vault
/// </summary>
public class MockKeyVaultService : IKeyVaultService
{
    private readonly Dictionary<string, string> _secrets = new();
    private readonly ILogger<MockKeyVaultService> _logger;

    public MockKeyVaultService(ILogger<MockKeyVaultService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using MockKeyVaultService - secrets will not be persisted");
    }

    public Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        _secrets.TryGetValue(secretName, out var value);
        return Task.FromResult(value);
    }

    public Task SetSecretAsync(string secretName, string value, DateTimeOffset? expiresOn = null, CancellationToken ct = default)
    {
        _secrets[secretName] = value;
        _logger.LogInformation("Mock: Stored secret {SecretName}", secretName);
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
    {
        _secrets.Remove(secretName);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetSecretExpirationAsync(string secretName, CancellationToken ct = default)
    {
        return Task.FromResult<DateTimeOffset?>(null);
    }
}

/// <summary>
/// Mock Message Queue service for development without Azure Service Bus
/// </summary>
public class MockMessageQueueService : IMessageQueueService
{
    private readonly ILogger<MockMessageQueueService> _logger;

    public MockMessageQueueService(ILogger<MockMessageQueueService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using MockMessageQueueService - messages will be logged but not queued");
    }

    public Task SendAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class
    {
        _logger.LogInformation("Mock: Would send message to queue {QueueName}: {MessageType}",
            queueName, typeof(T).Name);
        return Task.CompletedTask;
    }

    public Task SendAsync<T>(string queueName, T message, TimeSpan delay, CancellationToken ct = default) where T : class
    {
        _logger.LogInformation("Mock: Would send delayed message to queue {QueueName} with delay {Delay}: {MessageType}",
            queueName, delay, typeof(T).Name);
        return Task.CompletedTask;
    }
}
