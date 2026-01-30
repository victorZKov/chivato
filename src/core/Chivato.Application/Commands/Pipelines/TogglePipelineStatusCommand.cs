using MediatR;

namespace Chivato.Application.Commands.Pipelines;

public record ActivatePipelineCommand(string Id) : IRequest<TogglePipelineResult>;

public record DeactivatePipelineCommand(string Id) : IRequest<TogglePipelineResult>;

public record TogglePipelineResult(bool Success, string NewStatus, string? Error = null);
