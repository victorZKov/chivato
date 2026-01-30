using Chivato.Application.Common;
using Chivato.Application.DTOs;
using Chivato.Application.Queries.Drift;
using Chivato.Domain.Interfaces;
using Chivato.Domain.ValueObjects;
using MediatR;

namespace Chivato.Application.Handlers.Drift;

public class GetDriftPagedHandler : IRequestHandler<GetDriftPagedQuery, PagedResultDto<DriftRecordDto>>
{
    private readonly IDriftRecordRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetDriftPagedHandler(IDriftRecordRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PagedResultDto<DriftRecordDto>> Handle(GetDriftPagedQuery request, CancellationToken cancellationToken)
    {
        var result = await _repository.GetPagedAsync(
            _currentUser.TenantId,
            request.Page,
            request.PageSize,
            request.Severity,
            request.PipelineId,
            request.From,
            request.To,
            cancellationToken
        );

        var items = result.Items.Select(d => new DriftRecordDto(
            d.Id,
            d.PipelineId,
            string.Empty, // PipelineName - would need to join
            d.Severity.ToDisplayString(),
            d.ResourceId,
            d.ResourceType,
            d.ResourceName,
            d.Property,
            d.ExpectedValue,
            d.ActualValue,
            d.Description,
            d.Recommendation,
            d.Category,
            d.DetectedAt,
            d.Status.ToString()
        )).ToList();

        return new PagedResultDto<DriftRecordDto>(items, result.Total, result.Page, result.PageSize);
    }
}

public class GetDriftByIdHandler : IRequestHandler<GetDriftByIdQuery, DriftRecordDto?>
{
    private readonly IDriftRecordRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetDriftByIdHandler(IDriftRecordRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<DriftRecordDto?> Handle(GetDriftByIdQuery request, CancellationToken cancellationToken)
    {
        var drift = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

        if (drift == null) return null;

        return new DriftRecordDto(
            drift.Id,
            drift.PipelineId,
            string.Empty,
            drift.Severity.ToDisplayString(),
            drift.ResourceId,
            drift.ResourceType,
            drift.ResourceName,
            drift.Property,
            drift.ExpectedValue,
            drift.ActualValue,
            drift.Description,
            drift.Recommendation,
            drift.Category,
            drift.DetectedAt,
            drift.Status.ToString()
        );
    }
}

public class GetDriftStatsHandler : IRequestHandler<GetDriftStatsQuery, DriftStatsDto>
{
    private readonly IDriftRecordRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetDriftStatsHandler(IDriftRecordRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<DriftStatsDto> Handle(GetDriftStatsQuery request, CancellationToken cancellationToken)
    {
        var stats = await _repository.GetStatsAsync(_currentUser.TenantId, cancellationToken);

        return new DriftStatsDto(
            stats.Total,
            stats.Critical,
            stats.High,
            stats.Medium,
            stats.Low,
            stats.LastAnalysis
        );
    }
}
