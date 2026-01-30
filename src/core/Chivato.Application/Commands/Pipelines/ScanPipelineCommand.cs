using MediatR;

namespace Chivato.Application.Commands.Pipelines;

/// <summary>
/// Triggers a drift scan for a specific pipeline
/// </summary>
public record ScanPipelineCommand(string PipelineId) : IRequest<ScanPipelineResult>;

public record ScanPipelineResult(
    string CorrelationId,
    bool Success,
    string? Error = null
);
