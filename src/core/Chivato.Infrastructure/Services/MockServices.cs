using System.Text.Json;
using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chivato.Infrastructure.Services;

/// <summary>
/// Mock Key Vault service for development without Azure Key Vault
/// Persists secrets to a JSON file for container restarts
/// </summary>
public class MockKeyVaultService : IKeyVaultService
{
    private readonly Dictionary<string, string> _secrets = new();
    private readonly ILogger<MockKeyVaultService> _logger;
    private readonly string _secretsFilePath;
    private readonly object _lock = new();

    public MockKeyVaultService(ILogger<MockKeyVaultService> logger)
    {
        _logger = logger;

        // Use /app/data for Docker, fallback to temp for local dev
        var dataDir = Directory.Exists("/app/data") ? "/app/data" : Path.GetTempPath();
        _secretsFilePath = Path.Combine(dataDir, "mock-secrets.json");

        LoadSecrets();
        _logger.LogInformation("MockKeyVaultService initialized with {Count} secrets from {Path}",
            _secrets.Count, _secretsFilePath);
    }

    private void LoadSecrets()
    {
        try
        {
            if (File.Exists(_secretsFilePath))
            {
                var json = File.ReadAllText(_secretsFilePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                    {
                        _secrets[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load secrets from {Path}", _secretsFilePath);
        }
    }

    private void SaveSecrets()
    {
        try
        {
            var dir = Path.GetDirectoryName(_secretsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_secrets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_secretsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save secrets to {Path}", _secretsFilePath);
        }
    }

    public Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _secrets.TryGetValue(secretName, out var value);
            return Task.FromResult(value);
        }
    }

    public Task SetSecretAsync(string secretName, string value, DateTimeOffset? expiresOn = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _secrets[secretName] = value;
            SaveSecrets();
            _logger.LogInformation("Mock: Stored secret {SecretName}", secretName);
        }
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _secrets.Remove(secretName);
            SaveSecrets();
            _logger.LogInformation("Mock: Deleted secret {SecretName}", secretName);
        }
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
