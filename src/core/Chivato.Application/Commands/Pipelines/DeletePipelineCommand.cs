using MediatR;

namespace Chivato.Application.Commands.Pipelines;

public record DeletePipelineCommand(string Id) : IRequest<DeletePipelineResult>;

public record DeletePipelineResult(bool Success, string? ErrorMessage = null);
