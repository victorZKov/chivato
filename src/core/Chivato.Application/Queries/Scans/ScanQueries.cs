using MediatR;

namespace Chivato.Application.Queries.Scans;

public record GetScansPagedQuery(
    int Page,
    int PageSize,
    string? PipelineId = null,
    string? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<PagedScanResult>;

public record GetScanByIdQuery(string Id) : IRequest<ScanDetailDto?>;

public record GetScanStatsQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<ScanStatsDto>;

// DTOs
public record PagedScanResult(
    IEnumerable<ScanDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record ScanDto(
    string Id,
    string PipelineId,
    string? PipelineName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    int? DriftCount,
    int? DurationSeconds,
    string? TriggeredBy,
    string? ErrorMessage,
    int? ResourcesScanned
);

public record ScanDetailDto(
    string Id,
    string PipelineId,
    string? PipelineName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    int? DriftCount,
    int? DurationSeconds,
    string? TriggeredBy,
    string? ErrorMessage,
    int? ResourcesScanned,
    string? CorrelationId,
    IEnumerable<ScanStepDto>? Steps
);

public record ScanStepDto(
    string Name,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Message
);

public record ScanStatsDto(
    int Total,
    int Success,
    int Failed,
    double? AvgDurationSeconds
);
