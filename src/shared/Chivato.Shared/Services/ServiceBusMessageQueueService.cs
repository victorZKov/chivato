using Azure.Messaging.ServiceBus;
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
    private const string QueueName = "drift-analysis-requests";

    public ServiceBusMessageQueueService(string connectionString)
    {
        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(QueueName);
    }

    public async Task SendAnalysisMessageAsync(DriftAnalysisMessage message)
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

    public async Task SendAnalysisMessagesAsync(IEnumerable<DriftAnalysisMessage> messages)
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
