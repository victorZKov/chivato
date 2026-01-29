using Chivato.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/drift")]
public class DriftController : ControllerBase
{
    private readonly IStorageService _storageService;

    public DriftController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDriftRecords(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? pipelineId,
        [FromQuery] string? severity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var drifts = await _storageService.GetDriftRecordsAsync(from, to, pipelineId, severity);
        var driftList = drifts.ToList();

        var total = driftList.Count;
        var items = driftList.Skip((page - 1) * pageSize).Take(pageSize);

        return Ok(new
        {
            items = items.Select(d => new
            {
                id = d.RowKey,
                pipelineId = d.PipelineId,
                pipelineName = d.PipelineName,
                severity = d.Severity,
                resourceId = d.ResourceId,
                resourceType = d.ResourceType,
                resourceName = d.ResourceName,
                property = d.Property,
                expectedValue = d.ExpectedValue,
                actualValue = d.ActualValue,
                description = d.Description,
                recommendation = d.Recommendation,
                category = d.Category,
                detectedAt = d.DetectedAt
            }),
            total,
            page,
            pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDriftRecord(string id)
    {
        var drift = await _storageService.GetDriftRecordAsync(id);
        if (drift == null)
            return NotFound();

        return Ok(new
        {
            id = drift.RowKey,
            pipelineId = drift.PipelineId,
            pipelineName = drift.PipelineName,
            severity = drift.Severity,
            resourceId = drift.ResourceId,
            resourceType = drift.ResourceType,
            resourceName = drift.ResourceName,
            property = drift.Property,
            expectedValue = drift.ExpectedValue,
            actualValue = drift.ActualValue,
            driftType = drift.DriftType,
            description = drift.Description,
            recommendation = drift.Recommendation,
            category = drift.Category,
            detectedAt = drift.DetectedAt
        });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetDriftStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var stats = await _storageService.GetDriftStatsAsync(from, to);

        return Ok(new
        {
            total = stats.Total,
            critical = stats.Critical,
            high = stats.High,
            medium = stats.Medium,
            low = stats.Low,
            lastAnalysis = stats.LastAnalysis
        });
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportDrifts([FromBody] ExportDriftsInput input)
    {
        var drifts = await _storageService.GetDriftRecordsAsync(
            input.From, input.To, input.PipelineId, input.Severity);

        var driftList = drifts.ToList();

        if (input.Format?.ToLower() == "csv")
        {
            var csv = "Id,PipelineId,PipelineName,Severity,ResourceId,ResourceType,ResourceName,Property,ExpectedValue,ActualValue,Description,Category,DetectedAt\n";
            csv += string.Join("\n", driftList.Select(d =>
                $"\"{d.RowKey}\",\"{d.PipelineId}\",\"{d.PipelineName}\",\"{d.Severity}\",\"{d.ResourceId}\",\"{d.ResourceType}\",\"{d.ResourceName}\",\"{d.Property}\",\"{EscapeCsv(d.ExpectedValue)}\",\"{EscapeCsv(d.ActualValue)}\",\"{EscapeCsv(d.Description)}\",\"{d.Category}\",\"{d.DetectedAt:O}\""
            ));

            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"drift-export-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // Default to JSON
        return Ok(driftList.Select(d => new
        {
            id = d.RowKey,
            pipelineId = d.PipelineId,
            pipelineName = d.PipelineName,
            severity = d.Severity,
            resourceId = d.ResourceId,
            resourceType = d.ResourceType,
            resourceName = d.ResourceName,
            property = d.Property,
            expectedValue = d.ExpectedValue,
            actualValue = d.ActualValue,
            description = d.Description,
            recommendation = d.Recommendation,
            category = d.Category,
            detectedAt = d.DetectedAt
        }));
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }
}

public record ExportDriftsInput(
    DateTime? From,
    DateTime? To,
    string? PipelineId,
    string? Severity,
    string? Format);
