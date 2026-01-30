using Chivato.Application.Queries.Scans;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/scans")]
public class ScansController : ControllerBase
{
    private readonly IMediator _mediator;

    public ScansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get scan logs with filtering and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedScanResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScans(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? pipelineId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetScansPagedQuery(page, pageSize, pipelineId, status, from, to);
        var result = await _mediator.Send(query);

        return Ok(new
        {
            items = result.Items,
            total = result.Total,
            page = result.Page,
            pageSize = result.PageSize
        });
    }

    /// <summary>
    /// Get a specific scan by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ScanDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScan(string id)
    {
        var query = new GetScanByIdQuery(id);
        var result = await _mediator.Send(query);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Get scan statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ScanStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScanStats(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var query = new GetScanStatsQuery(from, to);
        var result = await _mediator.Send(query);

        return Ok(new
        {
            total = result.Total,
            success = result.Success,
            failed = result.Failed,
            avgDurationSeconds = result.AvgDurationSeconds
        });
    }
}
