using Chivato.Application.Commands.Pipelines;
using Chivato.Application.DTOs;
using Chivato.Application.Queries.Pipelines;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers.V2;

/// <summary>
/// Pipelines API using CQRS pattern with MediatR
/// </summary>
[ApiController]
[Route("api/v2/pipelines")]
public class PipelinesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PipelinesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all pipelines for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PipelineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPipelinesQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific pipeline by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PipelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPipelineByIdQuery(id), ct);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Create a new pipeline
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePipelineResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePipelineCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.PipelineId }, result);
    }

    /// <summary>
    /// Update an existing pipeline
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdatePipelineRequest request, CancellationToken ct)
    {
        var command = new UpdatePipelineCommand(
            id,
            request.Name ?? "",
            request.Branch ?? "",
            request.TerraformPath ?? "",
            request.SubscriptionId ?? "",
            request.ResourceGroup ?? ""
        );
        var result = await _mediator.Send(command, ct);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found") == true)
                return NotFound();
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Delete a pipeline
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeletePipelineCommand(id), ct);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found") == true)
                return NotFound();
            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// Activate a pipeline
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ActivatePipelineCommand(id), ct);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found") == true)
                return NotFound();
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { success = true, status = result.NewStatus });
    }

    /// <summary>
    /// Deactivate a pipeline
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeactivatePipelineCommand(id), ct);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found") == true)
                return NotFound();
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { success = true, status = result.NewStatus });
    }

    /// <summary>
    /// Trigger a scan for a specific pipeline
    /// </summary>
    [HttpPost("{id}/scan")]
    [ProducesResponseType(typeof(ScanPipelineResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Scan(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ScanPipelineCommand(id), ct);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found") == true)
                return NotFound();
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Accepted(new { correlationId = result.CorrelationId });
    }
}

// Request DTOs
public record UpdatePipelineRequest(
    string? Name,
    string? Branch,
    string? TerraformPath,
    string? SubscriptionId,
    string? ResourceGroup
);
