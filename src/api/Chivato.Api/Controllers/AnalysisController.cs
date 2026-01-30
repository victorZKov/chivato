using Chivato.Application.Commands.Analysis;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(IMediator mediator, ILogger<AnalysisController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Trigger drift analysis for a specific pipeline or all pipelines
    /// </summary>
    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TriggerAnalysis([FromBody] TriggerAnalysisRequest? request)
    {
        var command = new TriggerAnalysisCommand(
            PipelineId: request?.PipelineId,
            AnalyzeAll: false
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
                return NotFound(new { error = result.Error });

            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Triggered analysis: {CorrelationId}", result.CorrelationId);

        return Accepted(new
        {
            correlationId = result.CorrelationId,
            status = "queued",
            message = "Analysis request has been queued for processing"
        });
    }

    /// <summary>
    /// Trigger drift analysis for all active pipelines
    /// </summary>
    [HttpPost("trigger-all")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerAllAnalysis()
    {
        var command = new TriggerAnalysisCommand(
            PipelineId: null,
            AnalyzeAll: true
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        _logger.LogInformation("Triggered analysis for all pipelines: {CorrelationId}", result.CorrelationId);

        return Accepted(new
        {
            correlationId = result.CorrelationId,
            status = "queued",
            message = "Analysis request for all pipelines has been queued"
        });
    }
}

// Request DTOs
public record TriggerAnalysisRequest(string? PipelineId);
