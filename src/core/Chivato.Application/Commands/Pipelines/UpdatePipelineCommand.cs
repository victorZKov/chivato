using MediatR;

namespace Chivato.Application.Commands.Pipelines;

public record UpdatePipelineCommand(
    string Id,
    string Name,
    string Branch,
    string TerraformPath,
    string SubscriptionId,
    string ResourceGroup
) : IRequest<UpdatePipelineResult>;

public record UpdatePipelineResult(bool Success, string? Error = null);
