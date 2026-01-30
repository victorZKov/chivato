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
