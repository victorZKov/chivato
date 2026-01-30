using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chivato.Infrastructure.Services;

public class MessageQueueService : IMessageQueueService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<MessageQueueService> _logger;
    private readonly Dictionary<string, ServiceBusSender> _senders = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MessageQueueService(ServiceBusClient client, ILogger<MessageQueueService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task SendAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class
    {
        try
        {
            var sender = await GetOrCreateSenderAsync(queueName);
            var json = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString()
            };

            await sender.SendMessageAsync(serviceBusMessage, ct);

            _logger.LogDebug("Message sent to queue {QueueName}: {MessageId}",
                queueName, serviceBusMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task SendAsync<T>(string queueName, T message, TimeSpan delay, CancellationToken ct = default) where T : class
    {
        try
        {
            var sender = await GetOrCreateSenderAsync(queueName);
            var json = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                ScheduledEnqueueTime = DateTimeOffset.UtcNow.Add(delay)
            };

            var sequenceNumber = await sender.ScheduleMessageAsync(serviceBusMessage, serviceBusMessage.ScheduledEnqueueTime, ct);

            _logger.LogDebug("Message scheduled to queue {QueueName} with delay {Delay}: {MessageId} (seq: {SequenceNumber})",
                queueName, delay, serviceBusMessage.MessageId, sequenceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling message to queue {QueueName} with delay {Delay}", queueName, delay);
            throw;
        }
    }

    private async Task<ServiceBusSender> GetOrCreateSenderAsync(string queueName)
    {
        if (_senders.TryGetValue(queueName, out var existingSender))
        {
            return existingSender;
        }

        await _lock.WaitAsync();
        try
        {
            if (_senders.TryGetValue(queueName, out existingSender))
            {
                return existingSender;
            }

            var sender = _client.CreateSender(queueName);
            _senders[queueName] = sender;
            return sender;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
        _senders.Clear();

        await _client.DisposeAsync();
    }
}
