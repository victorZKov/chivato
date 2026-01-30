using Chivato.Application.Commands.Pipelines;
using Chivato.Application.DTOs;
using Chivato.Application.Queries.Pipelines;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/pipelines")]
public class PipelinesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(IMediator mediator, ILogger<PipelinesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PipelineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPipelines()
    {
        var result = await _mediator.Send(new GetPipelinesQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PipelineDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPipeline(string id)
    {
        var result = await _mediator.Send(new GetPipelineByIdQuery(id));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatePipelineResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePipeline([FromBody] CreatePipelineRequest request)
    {
        var command = new CreatePipelineCommand(
            Name: request.Name,
            Organization: request.Organization,
            Project: request.Project,
            RepositoryId: request.RepositoryId,
            Branch: request.Branch ?? "main",
            TerraformPath: request.TerraformPath,
            SubscriptionId: request.SubscriptionId,
            ResourceGroup: request.ResourceGroup
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(nameof(GetPipeline), new { id = result.PipelineId }, result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UpdatePipelineResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePipeline(string id, [FromBody] UpdatePipelineRequest request)
    {
        var command = new UpdatePipelineCommand(
            Id: id,
            Name: request.Name,
            Branch: request.Branch,
            TerraformPath: request.TerraformPath,
            SubscriptionId: request.SubscriptionId,
            ResourceGroup: request.ResourceGroup
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Pipeline not found")
                return NotFound();

            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePipeline(string id)
    {
        var command = new DeletePipelineCommand(id);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Pipeline not found")
                return NotFound();

            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [ProducesResponseType(typeof(TogglePipelineResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivatePipeline(string id)
    {
        var command = new ActivatePipelineCommand(id);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Pipeline not found")
                return NotFound();

            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result);
    }

    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(typeof(TogglePipelineResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePipeline(string id)
    {
        var command = new DeactivatePipelineCommand(id);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Pipeline not found")
                return NotFound();

            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result);
    }

    [HttpPost("{id}/scan")]
    [ProducesResponseType(typeof(ScanPipelineResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerScan(string id)
    {
        var command = new ScanPipelineCommand(id);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Pipeline not found")
                return NotFound();

            return BadRequest(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("Triggered scan for pipeline {PipelineId}, correlationId: {CorrelationId}",
            id, result.CorrelationId);

        return Accepted(new
        {
            correlationId = result.CorrelationId,
            message = "Scan queued"
        });
    }
}

// Request DTOs
public record CreatePipelineRequest(
    string Name,
    string Organization,
    string Project,
    string RepositoryId,
    string? Branch,
    string TerraformPath,
    string SubscriptionId,
    string ResourceGroup
);

public record UpdatePipelineRequest(
    string Name,
    string Branch,
    string TerraformPath,
    string SubscriptionId,
    string ResourceGroup
);
