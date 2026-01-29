using Chivato.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/scans")]
public class ScansController : ControllerBase
{
    private readonly IStorageService _storageService;

    public ScansController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetScans(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? pipelineId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var scans = await _storageService.GetScanLogsAsync(from, to, pipelineId, status);
        var scanList = scans.ToList();

        var total = scanList.Count;
        var items = scanList.Skip((page - 1) * pageSize).Take(pageSize);

        return Ok(new
        {
            items = items.Select(s => new
            {
                id = s.RowKey,
                pipelineId = s.PipelineId,
                pipelineName = s.PipelineName,
                startedAt = s.StartedAt,
                completedAt = s.CompletedAt,
                status = s.Status,
                driftCount = s.DriftCount,
                durationSeconds = s.DurationSeconds,
                triggeredBy = s.TriggeredBy,
                errorMessage = s.ErrorMessage,
                resourcesScanned = s.ResourcesScanned
            }),
            total,
            page,
            pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetScan(string id)
    {
        var scan = await _storageService.GetScanLogAsync(id);
        if (scan == null)
            return NotFound();

        // Parse steps if available
        List<object>? steps = null;
        if (!string.IsNullOrEmpty(scan.StepsJson))
        {
            try
            {
                steps = System.Text.Json.JsonSerializer.Deserialize<List<object>>(scan.StepsJson);
            }
            catch { }
        }

        return Ok(new
        {
            id = scan.RowKey,
            pipelineId = scan.PipelineId,
            pipelineName = scan.PipelineName,
            startedAt = scan.StartedAt,
            completedAt = scan.CompletedAt,
            status = scan.Status,
            driftCount = scan.DriftCount,
            durationSeconds = scan.DurationSeconds,
            triggeredBy = scan.TriggeredBy,
            errorMessage = scan.ErrorMessage,
            resourcesScanned = scan.ResourcesScanned,
            correlationId = scan.CorrelationId,
            steps
        });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetScanStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var stats = await _storageService.GetScanStatsAsync(from, to);

        return Ok(new
        {
            total = stats.Total,
            success = stats.Success,
            failed = stats.Failed,
            avgDurationSeconds = stats.AvgDurationSeconds
        });
    }
}
