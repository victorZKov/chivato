using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/pipelines")]
public class PipelinesController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IMessageQueueService? _messageQueueService;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(
        IStorageService storageService,
        IMessageQueueService? messageQueueService,
        ILogger<PipelinesController> logger)
    {
        _storageService = storageService;
        _messageQueueService = messageQueueService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetPipelines()
    {
        var pipelines = await _storageService.GetAllPipelinesAsync();
        var adoConnections = await _storageService.GetAdoConnectionsAsync();
        var azureConnections = await _storageService.GetAzureConnectionsAsync();

        var result = pipelines.Select(p => new
        {
            id = p.PipelineId,
            pipelineName = p.PipelineName,
            pipelineId = p.PipelineId,
            projectName = p.ProjectName,
            organizationUrl = p.OrganizationUrl,
            adoConnectionId = p.AdoConnectionId,
            adoConnectionName = adoConnections.FirstOrDefault(c => c.RowKey == p.AdoConnectionId)?.Name ?? "",
            azureConnectionId = p.AzureConnectionId,
            azureConnectionName = azureConnections.FirstOrDefault(c => c.RowKey == p.AzureConnectionId)?.Name ?? "",
            isActive = p.IsActive,
            lastScanAt = p.LastScanAt,
            driftCount = p.DriftCount
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPipeline(string id)
    {
        var pipeline = await _storageService.GetPipelineByIdAsync(id);
        if (pipeline == null)
            return NotFound();

        var recentDrifts = (await _storageService.GetDriftRecordsByPipelineAsync(id)).Take(5);
        var recentScans = await _storageService.GetScanLogsByPipelineAsync(id, 5);

        var adoConnections = await _storageService.GetAdoConnectionsAsync();
        var azureConnections = await _storageService.GetAzureConnectionsAsync();

        return Ok(new
        {
            id = pipeline.PipelineId,
            pipelineName = pipeline.PipelineName,
            pipelineId = pipeline.PipelineId,
            projectName = pipeline.ProjectName,
            organizationUrl = pipeline.OrganizationUrl,
            adoConnectionId = pipeline.AdoConnectionId,
            adoConnectionName = adoConnections.FirstOrDefault(c => c.RowKey == pipeline.AdoConnectionId)?.Name ?? "",
            azureConnectionId = pipeline.AzureConnectionId,
            azureConnectionName = azureConnections.FirstOrDefault(c => c.RowKey == pipeline.AzureConnectionId)?.Name ?? "",
            isActive = pipeline.IsActive,
            lastScanAt = pipeline.LastScanAt,
            driftCount = pipeline.DriftCount,
            recentDrifts = recentDrifts.Select(d => new
            {
                id = d.RowKey,
                severity = d.Severity,
                resourceName = d.ResourceName,
                description = d.Description,
                detectedAt = d.DetectedAt
            }),
            recentScans = recentScans.Select(s => new
            {
                id = s.RowKey,
                status = s.Status,
                startedAt = s.StartedAt,
                driftCount = s.DriftCount,
                durationSeconds = s.DurationSeconds
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePipeline([FromBody] CreatePipelineInput input)
    {
        var adoConnection = await _storageService.GetAdoConnectionAsync(input.AdoConnectionId);
        if (adoConnection == null)
            return BadRequest("Invalid ADO connection");

        // Create pipeline entity for each selected pipeline
        var created = new List<string>();

        foreach (var pipelineId in input.PipelineIds)
        {
            var entity = new PipelineEntity
            {
                PartitionKey = ExtractOrgId(adoConnection.OrganizationUrl),
                RowKey = pipelineId,
                PipelineId = pipelineId,
                PipelineName = input.PipelineNames?.GetValueOrDefault(pipelineId) ?? pipelineId,
                ProjectName = input.ProjectName,
                OrganizationUrl = adoConnection.OrganizationUrl,
                AdoConnectionId = input.AdoConnectionId,
                AzureConnectionId = input.AzureConnectionId,
                IsActive = true
            };

            await _storageService.SavePipelineAsync(entity);
            created.Add(pipelineId);
        }

        return Ok(new { created = created.Count, pipelineIds = created });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePipeline(string id, [FromBody] UpdatePipelineInput input)
    {
        var pipeline = await _storageService.GetPipelineByIdAsync(id);
        if (pipeline == null)
            return NotFound();

        if (input.IsActive.HasValue)
            pipeline.IsActive = input.IsActive.Value;

        if (!string.IsNullOrEmpty(input.AzureConnectionId))
            pipeline.AzureConnectionId = input.AzureConnectionId;

        await _storageService.SavePipelineAsync(pipeline);

        return Ok(new { success = true });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePipeline(string id)
    {
        var pipeline = await _storageService.GetPipelineByIdAsync(id);
        if (pipeline == null)
            return NotFound();

        await _storageService.DeletePipelineAsync(pipeline.PartitionKey, pipeline.RowKey);

        return NoContent();
    }

    [HttpPost("{id}/scan")]
    public async Task<IActionResult> TriggerScan(string id)
    {
        var pipeline = await _storageService.GetPipelineByIdAsync(id);
        if (pipeline == null)
            return NotFound();

        if (_messageQueueService == null)
            return BadRequest("Message queue not configured");

        var message = new DriftAnalysisMessage
        {
            TriggerType = "AdHoc",
            PipelineId = id,
            OrganizationId = pipeline.PartitionKey,
            TenantId = pipeline.TenantId,
            Priority = "High",
            SendNotification = true
        };

        await _messageQueueService.SendAnalysisMessageAsync(message);

        _logger.LogInformation("Triggered scan for pipeline {PipelineId}", id);

        return Accepted(new
        {
            correlationId = message.CorrelationId,
            message = "Scan queued"
        });
    }

    [HttpGet("{id}/drifts")]
    public async Task<IActionResult> GetPipelineDrifts(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var drifts = await _storageService.GetDriftRecordsByPipelineAsync(id);
        var driftList = drifts.ToList();

        var total = driftList.Count;
        var items = driftList.Skip((page - 1) * pageSize).Take(pageSize);

        return Ok(new
        {
            items = items.Select(d => new
            {
                id = d.RowKey,
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

    [HttpGet("{id}/scans")]
    public async Task<IActionResult> GetPipelineScans(string id)
    {
        var scans = await _storageService.GetScanLogsByPipelineAsync(id, 50);

        return Ok(scans.Select(s => new
        {
            id = s.RowKey,
            startedAt = s.StartedAt,
            completedAt = s.CompletedAt,
            status = s.Status,
            driftCount = s.DriftCount,
            durationSeconds = s.DurationSeconds,
            triggeredBy = s.TriggeredBy,
            errorMessage = s.ErrorMessage
        }));
    }

    private static string ExtractOrgId(string organizationUrl)
    {
        // Extract org name from URL like https://dev.azure.com/myorg
        var uri = new Uri(organizationUrl);
        return uri.AbsolutePath.Trim('/');
    }
}

// Input DTOs
public record CreatePipelineInput(
    string AdoConnectionId,
    string AzureConnectionId,
    string ProjectName,
    string[] PipelineIds,
    Dictionary<string, string>? PipelineNames);

public record UpdatePipelineInput(bool? IsActive, string? AzureConnectionId);
