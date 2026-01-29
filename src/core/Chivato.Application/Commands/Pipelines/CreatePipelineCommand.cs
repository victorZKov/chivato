using MediatR;

namespace Chivato.Application.Commands.Pipelines;

public record CreatePipelineCommand(
    string Name,
    string Organization,
    string Project,
    string RepositoryId,
    string Branch,
    string TerraformPath,
    string SubscriptionId,
    string ResourceGroup
) : IRequest<CreatePipelineResult>;

public record CreatePipelineResult(string Id, bool Success, string? Error = null);
