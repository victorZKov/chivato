using Chivato.Shared.Models.Messages;

namespace Chivato.Shared.Services;

/// <summary>
/// Abstraction for message queue operations.
/// Allows switching between Azure Service Bus (enterprise) and RabbitMQ (SaaS).
/// </summary>
public interface IMessageQueueService
{
    /// <summary>
    /// Send a drift analysis message to the queue
    /// </summary>
    Task SendAnalysisMessageAsync(DriftAnalysisMessage message);

    /// <summary>
    /// Send multiple analysis messages (batch)
    /// </summary>
    Task SendAnalysisMessagesAsync(IEnumerable<DriftAnalysisMessage> messages);
}

/// <summary>
/// Handler for processing messages from the queue (used by worker)
/// </summary>
public interface IMessageHandler<T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
