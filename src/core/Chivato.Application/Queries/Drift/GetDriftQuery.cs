using Chivato.Application.DTOs;
using MediatR;

namespace Chivato.Application.Queries.Drift;

public record GetDriftPagedQuery(
    int Page = 1,
    int PageSize = 20,
    string? Severity = null,
    string? PipelineId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<PagedResultDto<DriftRecordDto>>;

public record GetDriftByIdQuery(string Id) : IRequest<DriftRecordDto?>;

public record GetDriftStatsQuery() : IRequest<DriftStatsDto>;

public record GetDriftByPipelineQuery(
    string PipelineId,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResultDto<DriftRecordDto>>;

public record ExportDriftQuery(
    ExportFormat Format,
    string? Severity = null,
    string? PipelineId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<ExportResult>;

public enum ExportFormat
{
    Csv,
    Json
}

public record ExportResult(
    byte[] Content,
    string ContentType,
    string FileName
);
