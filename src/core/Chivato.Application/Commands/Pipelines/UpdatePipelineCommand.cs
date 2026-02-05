using MediatR;

namespace Chivato.Application.Commands.Pipelines;

public record UpdatePipelineCommand(
    string Id,
    string? Name = null,
    string? Branch = null,
    string? TerraformPath = null,
    string? SubscriptionId = null,
    string? ResourceGroup = null,
    string? RepositoryName = null,
    string? PlanOnlyParameter = null,
    string? AdoConnectionId = null
) : IRequest<UpdatePipelineResult>;

public record UpdatePipelineResult(bool Success, string? ErrorMessage = null);
