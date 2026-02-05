using Chivato.Application.Common;
using Chivato.Application.DTOs;
using Chivato.Application.Queries.Scans;
using Chivato.Domain.Interfaces;
using Chivato.Domain.ValueObjects;
using MediatR;

namespace Chivato.Application.Handlers.Scans;

public class GetScansPagedHandler : IRequestHandler<GetScansPagedQuery, PagedScanResult>
{
    private readonly IScanLogRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetScansPagedHandler(IScanLogRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PagedScanResult> Handle(GetScansPagedQuery request, CancellationToken cancellationToken)
    {
        var result = await _repository.GetPagedAsync(
            _currentUser.TenantId,
            request.Page,
            request.PageSize,
            request.Status,
            request.PipelineId,
            request.From,
            request.To,
            cancellationToken
        );

        var items = result.Items.Select(s => new ScanDto(
            Id: s.Id,
            PipelineId: s.PipelineId,
            PipelineName: s.PipelineName,
            StartedAt: s.StartedAt,
            CompletedAt: s.CompletedAt,
            Status: s.Status.ToString().ToLower(),
            DriftCount: s.DriftCount,
            DurationSeconds: s.DurationSeconds,
            TriggeredBy: s.TriggeredBy,
            ErrorMessage: s.ErrorMessage,
            ResourcesScanned: s.ResourcesScanned
        ));

        return new PagedScanResult(items, result.Total, request.Page, request.PageSize);
    }
}

public class GetScanByIdHandler : IRequestHandler<GetScanByIdQuery, ScanDetailDto?>
{
    private readonly IScanLogRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetScanByIdHandler(IScanLogRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ScanDetailDto?> Handle(GetScanByIdQuery request, CancellationToken cancellationToken)
    {
        var scan = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

        if (scan == null)
            return null;

        return new ScanDetailDto(
            Id: scan.Id,
            PipelineId: scan.PipelineId,
            PipelineName: scan.PipelineName,
            StartedAt: scan.StartedAt,
            CompletedAt: scan.CompletedAt,
            Status: scan.Status.ToString().ToLower(),
            DriftCount: scan.DriftCount,
            DurationSeconds: scan.DurationSeconds,
            TriggeredBy: scan.TriggeredBy,
            ErrorMessage: scan.ErrorMessage,
            ResourcesScanned: scan.ResourcesScanned,
            CorrelationId: scan.CorrelationId,
            Steps: null // Steps would come from a separate query or stored differently
        );
    }
}

public class GetScanStatsHandler : IRequestHandler<GetScanStatsQuery, ScanStatsDto>
{
    private readonly IScanLogRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetScanStatsHandler(IScanLogRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ScanStatsDto> Handle(GetScanStatsQuery request, CancellationToken cancellationToken)
    {
        var stats = await _repository.GetStatsAsync(
            _currentUser.TenantId,
            cancellationToken
        );

        return new ScanStatsDto(
            Total: stats.Total,
            Success: stats.Success,
            Failed: stats.Failed,
            AvgDurationSeconds: stats.AvgDurationSeconds
        );
    }
}

public class GetScanDriftsHandler : IRequestHandler<GetScanDriftsQuery, IReadOnlyList<DriftRecordDto>>
{
    private readonly IScanLogRepository _scanRepository;
    private readonly IDriftRecordRepository _driftRepository;
    private readonly ICurrentUser _currentUser;

    public GetScanDriftsHandler(
        IScanLogRepository scanRepository,
        IDriftRecordRepository driftRepository,
        ICurrentUser currentUser)
    {
        _scanRepository = scanRepository;
        _driftRepository = driftRepository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DriftRecordDto>> Handle(GetScanDriftsQuery request, CancellationToken cancellationToken)
    {
        var scan = await _scanRepository.GetByIdAsync(_currentUser.TenantId, request.ScanId, cancellationToken);

        if (scan == null || string.IsNullOrEmpty(scan.CorrelationId))
            return [];

        var drifts = await _driftRepository.GetByCorrelationIdAsync(
            _currentUser.TenantId,
            scan.CorrelationId,
            cancellationToken
        );

        return drifts.Select(d => new DriftRecordDto(
            d.Id,
            d.PipelineId,
            string.Empty,
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
    }
}
