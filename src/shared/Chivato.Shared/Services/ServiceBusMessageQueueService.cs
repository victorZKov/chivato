using Azure.Messaging.ServiceBus;
using Chivato.Shared.Constants;
using Chivato.Shared.Models.Messages;
using System.Text.Json;

namespace Chivato.Shared.Services;

/// <summary>
/// Azure Service Bus implementation of IMessageQueueService.
/// For SaaS version, create RabbitMqMessageQueueService implementing the same interface.
/// </summary>
public class ServiceBusMessageQueueService : IMessageQueueService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusMessageQueueService(string connectionString)
    {
        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(QueueNames.IacAnalysisRequests);
    }

    public async Task SendAnalysisMessageAsync(IacAnalysisMessage message)
    {
        var messageId = $"{message.TriggerType}-{message.PipelineId ?? "all"}-{message.CorrelationId}";

        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message))
        {
            MessageId = messageId,
            CorrelationId = message.CorrelationId,
            ContentType = "application/json",
            Subject = "DriftAnalysis"
        };

        await _sender.SendMessageAsync(serviceBusMessage);
    }

    public async Task SendAnalysisMessagesAsync(IEnumerable<IacAnalysisMessage> messages)
    {
        var batch = await _sender.CreateMessageBatchAsync();

        foreach (var message in messages)
        {
            var messageId = $"{message.TriggerType}-{message.PipelineId ?? "all"}-{message.CorrelationId}";

            var serviceBusMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message))
            {
                MessageId = messageId,
                CorrelationId = message.CorrelationId,
                ContentType = "application/json",
                Subject = "DriftAnalysis"
            };

            if (!batch.TryAddMessage(serviceBusMessage))
            {
                // Batch is full, send it and create a new one
                await _sender.SendMessagesAsync(batch);
                batch = await _sender.CreateMessageBatchAsync();
                batch.TryAddMessage(serviceBusMessage);
            }
        }

        if (batch.Count > 0)
        {
            await _sender.SendMessagesAsync(batch);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
