using Chivato.Application.Commands.Analysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers.V2;

/// <summary>
/// Analysis API using CQRS pattern with MediatR
/// </summary>
[ApiController]
[Route("api/v2/analysis")]
[Authorize]
public class AnalysisController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalysisController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Trigger a drift analysis
    /// </summary>
    [HttpPost("trigger")]
    [ProducesResponseType(typeof(TriggerAnalysisResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Trigger([FromBody] TriggerAnalysisCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted(result);
    }

    /// <summary>
    /// Trigger analysis for a specific pipeline
    /// </summary>
    [HttpPost("trigger/{pipelineId}")]
    [ProducesResponseType(typeof(TriggerAnalysisResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerForPipeline(string pipelineId, CancellationToken ct)
    {
        var command = new TriggerAnalysisCommand(PipelineId: pipelineId);
        var result = await _mediator.Send(command, ct);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted(result);
    }
}
