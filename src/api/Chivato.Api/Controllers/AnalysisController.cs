using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IMessageQueueService? _messageQueueService;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IStorageService storageService,
        IMessageQueueService? messageQueueService,
        ILogger<AnalysisController> logger)
    {
        _storageService = storageService;
        _messageQueueService = messageQueueService;
        _logger = logger;
    }

    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerAnalysis([FromBody] TriggerAnalysisInput? input)
    {
        if (_messageQueueService == null)
            return BadRequest(new { error = "Message queue not configured" });

        // Validate pipeline if specified
        if (!string.IsNullOrEmpty(input?.PipelineId))
        {
            var pipeline = await _storageService.GetPipelineByIdAsync(input.PipelineId);
            if (pipeline == null)
                return NotFound(new { error = "Pipeline not found" });
        }

        var message = new DriftAnalysisMessage
        {
            TriggerType = "AdHoc",
            PipelineId = input?.PipelineId,
            TenantId = GetTenantId(),
            InitiatedBy = GetUserId(),
            Priority = "High",
            SendNotification = input?.SendNotification ?? true
        };

        // Track the analysis status
        var status = new AnalysisStatusEntity
        {
            RowKey = message.CorrelationId,
            Status = "queued",
            PipelineId = message.PipelineId,
            TenantId = message.TenantId,
            InitiatedBy = message.InitiatedBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _storageService.SaveAnalysisStatusAsync(status);

        await _messageQueueService.SendAnalysisMessageAsync(message);

        _logger.LogInformation("Enqueued analysis: {CorrelationId}", message.CorrelationId);

        return Accepted(new
        {
            correlationId = message.CorrelationId,
            status = "queued",
            message = "Analysis request has been queued for processing"
        });
    }

    [HttpPost("trigger-all")]
    public async Task<IActionResult> TriggerAllAnalysis()
    {
        if (_messageQueueService == null)
            return BadRequest(new { error = "Message queue not configured" });

        var pipelines = await _storageService.GetActivePipelinesAsync();
        var pipelineList = pipelines.ToList();

        if (pipelineList.Count == 0)
            return Ok(new { pipelinesQueued = 0, correlationIds = Array.Empty<string>() });

        var messages = pipelineList.Select(p => new DriftAnalysisMessage
        {
            TriggerType = "AdHoc",
            PipelineId = p.PipelineId,
            OrganizationId = p.PartitionKey,
            TenantId = p.TenantId,
            InitiatedBy = GetUserId(),
            Priority = "Normal",
            SendNotification = true
        }).ToList();

        // Track all analysis statuses
        foreach (var message in messages)
        {
            var status = new AnalysisStatusEntity
            {
                RowKey = message.CorrelationId,
                Status = "queued",
                PipelineId = message.PipelineId,
                TenantId = message.TenantId,
                InitiatedBy = message.InitiatedBy,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _storageService.SaveAnalysisStatusAsync(status);
        }

        await _messageQueueService.SendAnalysisMessagesAsync(messages);

        _logger.LogInformation("Enqueued analysis for {Count} pipelines", pipelineList.Count);

        return Accepted(new
        {
            pipelinesQueued = messages.Count,
            correlationIds = messages.Select(m => m.CorrelationId)
        });
    }

    [HttpGet("status/{correlationId}")]
    public async Task<IActionResult> GetAnalysisStatus(string correlationId)
    {
        var status = await _storageService.GetAnalysisStatusAsync(correlationId);

        if (status == null)
            return NotFound(new { error = "Analysis not found" });

        return Ok(new
        {
            correlationId = status.RowKey,
            status = status.Status,
            pipelineId = status.PipelineId,
            pipelineName = status.PipelineName,
            progress = status.Progress,
            currentStage = status.CurrentStage,
            message = status.Message,
            driftCount = status.DriftCount,
            errorMessage = status.ErrorMessage,
            createdAt = status.CreatedAt,
            completedAt = status.CompletedAt,
            durationSeconds = status.DurationSeconds
        });
    }

    private string GetTenantId()
    {
        // TODO: Extract from JWT token claims
        return Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "demo-tenant";
    }

    private string? GetUserId()
    {
        // TODO: Extract from JWT token claims
        return Request.Headers["X-User-Id"].FirstOrDefault();
    }
}

public record TriggerAnalysisInput(string? PipelineId, bool? SendNotification);
