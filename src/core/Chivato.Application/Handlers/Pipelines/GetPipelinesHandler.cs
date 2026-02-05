using Chivato.Application.Common;
using Chivato.Application.DTOs;
using Chivato.Application.Queries.Pipelines;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class GetPipelinesHandler : IRequestHandler<GetPipelinesQuery, IReadOnlyList<PipelineDto>>
{
    private readonly IPipelineRepository _repository;
    private readonly IAdoConnectionRepository _adoConnectionRepository;
    private readonly IAzureConnectionRepository _azureConnectionRepository;
    private readonly ICurrentUser _currentUser;

    public GetPipelinesHandler(
        IPipelineRepository repository,
        IAdoConnectionRepository adoConnectionRepository,
        IAzureConnectionRepository azureConnectionRepository,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _adoConnectionRepository = adoConnectionRepository;
        _azureConnectionRepository = azureConnectionRepository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PipelineDto>> Handle(GetPipelinesQuery request, CancellationToken cancellationToken)
    {
        var pipelines = await _repository.GetAllAsync(_currentUser.TenantId, cancellationToken);

        // Fetch all connections to build lookup dictionaries
        var adoConnections = await _adoConnectionRepository.GetAllAsync(_currentUser.TenantId, cancellationToken);
        var azureConnections = await _azureConnectionRepository.GetAllAsync(_currentUser.TenantId, cancellationToken);

        var adoConnectionDict = adoConnections.ToDictionary(c => c.Id, c => c);
        var azureConnectionDict = azureConnections.ToDictionary(c => c.Id, c => c);

        return pipelines.Select(p => MapToDto(p, adoConnectionDict, azureConnectionDict)).ToList();
    }

    private static PipelineDto MapToDto(
        Pipeline p,
        Dictionary<string, AdoConnection> adoConnections,
        Dictionary<string, AzureConnection> azureConnections)
    {
        var adoConnectionName = p.AdoConnectionId != null && adoConnections.TryGetValue(p.AdoConnectionId, out var adoConn)
            ? adoConn.Name
            : string.Empty;

        var azureConnectionName = p.AzureConnectionId != null && azureConnections.TryGetValue(p.AzureConnectionId, out var azureConn)
            ? azureConn.Name
            : string.Empty;

        return new PipelineDto(
            Id: p.Id,
            PipelineName: p.Name,
            PipelineId: p.PipelineId ?? string.Empty,
            ProjectName: p.Project,
            OrganizationUrl: $"https://dev.azure.com/{p.Organization}",
            AdoConnectionId: p.AdoConnectionId ?? string.Empty,
            AdoConnectionName: adoConnectionName,
            AzureConnectionId: p.AzureConnectionId ?? string.Empty,
            AzureConnectionName: azureConnectionName,
            IsActive: p.Status == PipelineStatus.Active,
            LastScanAt: p.LastScanAt,
            DriftCount: p.DriftCount,
            Branch: p.Branch,
            RepositoryName: p.RepositoryName,
            PlanOnlyParameter: p.PlanOnlyParameter
        );
    }
}

public class GetPipelineByIdHandler : IRequestHandler<GetPipelineByIdQuery, PipelineDetailDto?>
{
    private readonly IPipelineRepository _repository;
    private readonly IAdoConnectionRepository _adoConnectionRepository;
    private readonly IAzureConnectionRepository _azureConnectionRepository;
    private readonly ICurrentUser _currentUser;

    public GetPipelineByIdHandler(
        IPipelineRepository repository,
        IAdoConnectionRepository adoConnectionRepository,
        IAzureConnectionRepository azureConnectionRepository,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _adoConnectionRepository = adoConnectionRepository;
        _azureConnectionRepository = azureConnectionRepository;
        _currentUser = currentUser;
    }

    public async Task<PipelineDetailDto?> Handle(GetPipelineByIdQuery request, CancellationToken cancellationToken)
    {
        var pipeline = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

        if (pipeline == null) return null;

        var adoConnectionName = string.Empty;
        var azureConnectionName = string.Empty;

        if (pipeline.AdoConnectionId != null)
        {
            var adoConn = await _adoConnectionRepository.GetByIdAsync(_currentUser.TenantId, pipeline.AdoConnectionId, cancellationToken);
            adoConnectionName = adoConn?.Name ?? string.Empty;
        }

        if (pipeline.AzureConnectionId != null)
        {
            var azureConn = await _azureConnectionRepository.GetByIdAsync(_currentUser.TenantId, pipeline.AzureConnectionId, cancellationToken);
            azureConnectionName = azureConn?.Name ?? string.Empty;
        }

        return new PipelineDetailDto(
            Id: pipeline.Id,
            PipelineName: pipeline.Name,
            PipelineId: pipeline.PipelineId ?? string.Empty,
            ProjectName: pipeline.Project,
            OrganizationUrl: $"https://dev.azure.com/{pipeline.Organization}",
            AdoConnectionId: pipeline.AdoConnectionId ?? string.Empty,
            AdoConnectionName: adoConnectionName,
            AzureConnectionId: pipeline.AzureConnectionId ?? string.Empty,
            AzureConnectionName: azureConnectionName,
            IsActive: pipeline.Status == PipelineStatus.Active,
            LastScanAt: pipeline.LastScanAt,
            LastScanStatus: pipeline.LastScanStatus,
            LastScanError: pipeline.LastScanError,
            DriftCount: pipeline.DriftCount,
            Branch: pipeline.Branch,
            RepositoryName: pipeline.RepositoryName,
            PlanOnlyParameter: pipeline.PlanOnlyParameter,
            CreatedAt: pipeline.CreatedAt,
            UpdatedAt: pipeline.UpdatedAt,
            RecentDrifts: null,
            RecentScans: null
        );
    }
}
