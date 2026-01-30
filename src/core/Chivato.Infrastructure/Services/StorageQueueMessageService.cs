using Azure.Storage.Queues;
using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Chivato.Infrastructure.Services;

/// <summary>
/// Message queue service using Azure Storage Queues (works with Azurite for development)
/// </summary>
public class StorageQueueMessageService : IMessageQueueService
{
    private readonly string _connectionString;
    private readonly ILogger<StorageQueueMessageService> _logger;
    private readonly Dictionary<string, QueueClient> _queueClients = new();
    private readonly object _lock = new();

    public StorageQueueMessageService(string connectionString, ILogger<StorageQueueMessageService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        _logger.LogInformation("Using StorageQueueMessageService (Azurite/Azure Storage Queue)");
    }

    private async Task<QueueClient> GetOrCreateQueueClientAsync(string queueName, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_queueClients.TryGetValue(queueName, out var existingClient))
                return existingClient;
        }

        var client = new QueueClient(_connectionString, queueName, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        });

        await client.CreateIfNotExistsAsync(cancellationToken: ct);

        lock (_lock)
        {
            _queueClients[queueName] = client;
        }

        _logger.LogInformation("Created queue client for {QueueName}", queueName);
        return client;
    }

    public async Task SendAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class
    {
        var client = await GetOrCreateQueueClientAsync(queueName, ct);

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await client.SendMessageAsync(json, ct);

        _logger.LogInformation("Sent message to queue {QueueName}: {MessageType}", queueName, typeof(T).Name);
    }

    public async Task SendAsync<T>(string queueName, T message, TimeSpan delay, CancellationToken ct = default) where T : class
    {
        var client = await GetOrCreateQueueClientAsync(queueName, ct);

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await client.SendMessageAsync(json, visibilityTimeout: delay, cancellationToken: ct);

        _logger.LogInformation("Sent delayed message to queue {QueueName} with delay {Delay}: {MessageType}",
            queueName, delay, typeof(T).Name);
    }
}
