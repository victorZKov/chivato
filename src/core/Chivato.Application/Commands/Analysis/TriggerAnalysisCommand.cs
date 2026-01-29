using MediatR;

namespace Chivato.Application.Commands.Analysis;

public record TriggerAnalysisCommand(
    string? PipelineId = null,
    bool AnalyzeAll = false
) : IRequest<TriggerAnalysisResult>;

public record TriggerAnalysisResult(
    string CorrelationId,
    bool Success,
    string? Error = null
);

/// <summary>
/// Interface for message queue service (Service Bus, RabbitMQ, etc.)
/// </summary>
public interface IMessageQueueService
{
    Task SendAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class;
}

/// <summary>
/// Message for drift analysis request
/// </summary>
public record DriftAnalysisMessage(
    string CorrelationId,
    string TenantId,
    string? PipelineId,
    bool AnalyzeAll,
    string TriggeredBy,
    DateTimeOffset QueuedAt
);
