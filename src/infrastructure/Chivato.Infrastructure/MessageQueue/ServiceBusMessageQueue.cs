using Azure.Messaging.ServiceBus;
using Chivato.Application.Commands.Analysis;
using System.Text.Json;

namespace Chivato.Infrastructure.MessageQueue;

/// <summary>
/// Azure Service Bus implementation of IMessageQueueService
/// </summary>
public class ServiceBusMessageQueue : IMessageQueueService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly Dictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusMessageQueue(string connectionString)
    {
        _client = new ServiceBusClient(connectionString);
    }

    public async Task SendAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class
    {
        var sender = GetOrCreateSender(queueName);

        var json = JsonSerializer.Serialize(message);
        var serviceBusMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
        };

        // Add correlation ID if available
        if (message is DriftAnalysisMessage analysisMessage)
        {
            serviceBusMessage.CorrelationId = analysisMessage.CorrelationId;
        }

        await sender.SendMessageAsync(serviceBusMessage, ct);
    }

    private ServiceBusSender GetOrCreateSender(string queueName)
    {
        if (!_senders.TryGetValue(queueName, out var sender))
        {
            sender = _client.CreateSender(queueName);
            _senders[queueName] = sender;
        }
        return sender;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
        await _client.DisposeAsync();
    }
}
