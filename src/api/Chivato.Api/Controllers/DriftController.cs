using Chivato.Application.Queries.Drift;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/drift")]
public class DriftController : ControllerBase
{
    private readonly IMediator _mediator;

    public DriftController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<DriftRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDriftRecords(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? pipelineId,
        [FromQuery] string? severity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetDriftPagedQuery(page, pageSize, severity, pipelineId, from, to);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DriftRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDriftRecord(string id)
    {
        var query = new GetDriftByIdQuery(id);
        var result = await _mediator.Send(query);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(DriftStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDriftStats()
    {
        var query = new GetDriftStatsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("pipeline/{pipelineId}")]
    [ProducesResponseType(typeof(PagedResultDto<DriftRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDriftsByPipeline(
        string pipelineId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetDriftByPipelineQuery(pipelineId, page, pageSize);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportDrifts([FromBody] ExportDriftsRequest request)
    {
        var format = request.Format?.ToLowerInvariant() == "csv" ? ExportFormat.Csv : ExportFormat.Json;

        var query = new ExportDriftQuery(
            format,
            request.Severity,
            request.PipelineId,
            request.From,
            request.To
        );

        var result = await _mediator.Send(query);

        return File(result.Content, result.ContentType, result.FileName);
    }
}

// Request DTOs
public record ExportDriftsRequest(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? PipelineId,
    string? Severity,
    string? Format
);

// DTOs (from Application layer)
public record PagedResultDto<T>(IEnumerable<T> Items, int Total, int Page, int PageSize);
public record DriftRecordDto(
    string Id,
    string PipelineId,
    string Severity,
    string ResourceId,
    string ResourceType,
    string ResourceName,
    string Property,
    string ExpectedValue,
    string ActualValue,
    string Description,
    string Recommendation,
    string Category,
    DateTimeOffset DetectedAt,
    string Status
);
public record DriftStatsDto(int Total, int Critical, int High, int Medium, int Low, DateTimeOffset? LastAnalysis);
