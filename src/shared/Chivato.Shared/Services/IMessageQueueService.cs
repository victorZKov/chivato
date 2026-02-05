using Chivato.Shared.Models.Messages;

namespace Chivato.Shared.Services;

/// <summary>
/// Abstraction for message queue operations.
/// Allows switching between Azure Service Bus (enterprise) and RabbitMQ (SaaS).
/// </summary>
public interface IMessageQueueService
{
    /// <summary>
    /// Send an IaC analysis message to the queue
    /// </summary>
    Task SendAnalysisMessageAsync(IacAnalysisMessage message);

    /// <summary>
    /// Send multiple IaC analysis messages (batch)
    /// </summary>
    Task SendAnalysisMessagesAsync(IEnumerable<IacAnalysisMessage> messages);
}

/// <summary>
/// Handler for processing messages from the queue (used by worker)
/// </summary>
public interface IMessageHandler<T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
