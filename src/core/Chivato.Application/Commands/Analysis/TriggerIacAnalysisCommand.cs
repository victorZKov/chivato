using MediatR;

namespace Chivato.Application.Commands.Analysis;

/// <summary>
/// Command to trigger IaC analysis (terraform plan) for a pipeline
/// </summary>
public record TriggerIacAnalysisCommand(
    string PipelineId,
    string IacType = "terraform"
) : IRequest<TriggerIacAnalysisResult>;

/// <summary>
/// Result of IaC analysis trigger
/// </summary>
public record TriggerIacAnalysisResult(
    string CorrelationId,
    bool Success,
    string? Error = null
);
