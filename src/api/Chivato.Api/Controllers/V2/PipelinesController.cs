using Chivato.Application.Commands.Pipelines;
using Chivato.Application.DTOs;
using Chivato.Application.Queries.Pipelines;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers.V2;

/// <summary>
/// Pipelines API using CQRS pattern with MediatR
/// </summary>
[ApiController]
[Route("api/v2/pipelines")]
[Authorize]
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
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
