using Chivato.Application.Commands.Credentials;
using Chivato.Application.Queries.Credentials;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/credentials")]
public class CredentialsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CredentialsController> _logger;

    public CredentialsController(IMediator mediator, ILogger<CredentialsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all credentials with their status
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CredentialStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCredentials()
    {
        var result = await _mediator.Send(new GetCredentialsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Get credentials expiring soon (within specified days)
    /// </summary>
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(IEnumerable<CredentialStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiringCredentials([FromQuery] int days = 30)
    {
        var result = await _mediator.Send(new GetExpiringCredentialsQuery(days));
        return Ok(result);
    }

    /// <summary>
    /// Test a credential connection
    /// </summary>
    [HttpPost("{type}/{id}/test")]
    [ProducesResponseType(typeof(TestCredentialResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestCredential(string type, string id)
    {
        var result = await _mediator.Send(new TestCredentialCommand(type, id));

        if (!result.Success && result.Error == "Credential not found")
            return NotFound();

        _logger.LogInformation("Credential test for {Type}/{Id}: {Result}", type, id, result.Success ? "Success" : "Failed");

        return Ok(new
        {
            success = result.Success,
            error = result.Error,
            testedAt = result.TestedAt
        });
    }

    /// <summary>
    /// Rotate a credential (update the secret)
    /// </summary>
    [HttpPut("{type}/{id}/rotate")]
    [ProducesResponseType(typeof(RotateCredentialResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateCredential(string type, string id, [FromBody] RotateCredentialRequest request)
    {
        if (string.IsNullOrEmpty(request.NewSecret))
            return BadRequest(new { error = "NewSecret is required" });

        var result = await _mediator.Send(new RotateCredentialCommand(type, id, request.NewSecret, request.ExpiresAt));

        if (!result.Success)
        {
            if (result.Error == "Credential not found")
                return NotFound();

            _logger.LogError("Error rotating credential {Type}/{Id}: {Error}", type, id, result.Error);
            return StatusCode(500, new { error = result.Error });
        }

        _logger.LogInformation("Credential rotated: {Type}/{Id}", type, id);

        return Ok(new
        {
            success = result.Success,
            rotatedAt = result.RotatedAt
        });
    }
}

// Request DTOs
public record RotateCredentialRequest(string NewSecret, DateTimeOffset? ExpiresAt);
