using Chivato.Application.Common;
using Chivato.Application.DTOs;
using Chivato.Application.Queries.Pipelines;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class GetPipelinesHandler : IRequestHandler<GetPipelinesQuery, IReadOnlyList<PipelineDto>>
{
    private readonly IPipelineRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetPipelinesHandler(IPipelineRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PipelineDto>> Handle(GetPipelinesQuery request, CancellationToken cancellationToken)
    {
        var pipelines = await _repository.GetAllAsync(_currentUser.TenantId, cancellationToken);

        return pipelines.Select(p => new PipelineDto(
            p.Id,
            p.Name,
            p.Organization,
            p.Project,
            p.RepositoryId,
            p.Branch,
            p.TerraformPath,
            p.SubscriptionId,
            p.ResourceGroup,
            p.Status.ToString(),
            p.LastScanAt,
            p.DriftCount,
            p.CreatedAt
        )).ToList();
    }
}

public class GetPipelineByIdHandler : IRequestHandler<GetPipelineByIdQuery, PipelineDto?>
{
    private readonly IPipelineRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetPipelineByIdHandler(IPipelineRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PipelineDto?> Handle(GetPipelineByIdQuery request, CancellationToken cancellationToken)
    {
        var pipeline = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

        if (pipeline == null) return null;

        return new PipelineDto(
            pipeline.Id,
            pipeline.Name,
            pipeline.Organization,
            pipeline.Project,
            pipeline.RepositoryId,
            pipeline.Branch,
            pipeline.TerraformPath,
            pipeline.SubscriptionId,
            pipeline.ResourceGroup,
            pipeline.Status.ToString(),
            pipeline.LastScanAt,
            pipeline.DriftCount,
            pipeline.CreatedAt
        );
    }
}
