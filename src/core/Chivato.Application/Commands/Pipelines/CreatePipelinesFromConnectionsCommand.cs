using MediatR;

namespace Chivato.Application.Commands.Pipelines;

/// <summary>
/// Command to create pipelines from connection references (matches UI model)
/// </summary>
public record CreatePipelinesFromConnectionsCommand(
    string AdoConnectionId,
    string AzureConnectionId,
    string ProjectName,
    IReadOnlyList<string> PipelineIds
) : IRequest<CreatePipelinesResult>;

public record CreatePipelinesResult(
    IReadOnlyList<string> CreatedPipelineIds,
    bool Success,
    string? ErrorMessage = null
);
