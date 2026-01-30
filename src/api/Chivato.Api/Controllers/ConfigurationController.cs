using Chivato.Application.Commands.Configuration;
using Chivato.Application.Queries.Configuration;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigurationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(IMediator mediator, ILogger<ConfigurationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    // Timer Configuration
    [HttpGet("timer")]
    [ProducesResponseType(typeof(ConfigurationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfiguration()
    {
        var result = await _mediator.Send(new GetConfigurationQuery());
        return Ok(result);
    }

    [HttpPut("timer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTimer([FromBody] UpdateTimerRequest request)
    {
        var command = new UpdateTimerCommand(request.IntervalHours);
        var result = await _mediator.Send(command);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    // Settings
    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        var command = new UpdateSettingsCommand(
            request.MinimumSeverityForAlert,
            request.MaxConcurrentScans,
            request.RetentionDays
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    // Azure Connections
    [HttpGet("azure")]
    [ProducesResponseType(typeof(IEnumerable<AzureConnectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAzureConnections()
    {
        var result = await _mediator.Send(new GetAzureConnectionsQuery());
        return Ok(result);
    }

    [HttpPost("azure")]
    [ProducesResponseType(typeof(SaveConnectionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAzureConnection([FromBody] CreateAzureConnectionRequest request)
    {
        var command = new SaveAzureConnectionCommand(
            Id: null,
            Name: request.Name,
            TenantId: request.TenantId,
            SubscriptionId: request.SubscriptionId,
            ClientId: request.ClientId,
            ClientSecret: request.ClientSecret
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Created Azure connection: {Name}", request.Name);

        return CreatedAtAction(nameof(GetAzureConnections), new { id = result.Id }, result);
    }

    // ADO Connections
    [HttpGet("ado")]
    [ProducesResponseType(typeof(IEnumerable<AdoConnectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdoConnections()
    {
        var result = await _mediator.Send(new GetAdoConnectionsQuery());
        return Ok(result);
    }

    [HttpPost("ado")]
    [ProducesResponseType(typeof(SaveConnectionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAdoConnection([FromBody] CreateAdoConnectionRequest request)
    {
        var command = new SaveAdoConnectionCommand(
            Id: null,
            Name: request.Name,
            Organization: request.Organization,
            Project: request.Project,
            PatToken: request.PatToken
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        _logger.LogInformation("Created ADO connection: {Name}", request.Name);

        return CreatedAtAction(nameof(GetAdoConnections), new { id = result.Id }, result);
    }

    // Email Recipients
    [HttpGet("recipients")]
    [ProducesResponseType(typeof(IEnumerable<EmailRecipientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecipients()
    {
        var result = await _mediator.Send(new GetEmailRecipientsQuery());
        return Ok(result);
    }

    [HttpPost("recipients")]
    [ProducesResponseType(typeof(SaveConnectionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddRecipient([FromBody] AddRecipientRequest request)
    {
        var command = new AddEmailRecipientCommand(
            Email: request.Email,
            Name: request.Name,
            MinimumSeverity: request.MinimumSeverity ?? "High"
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(nameof(GetRecipients), new { id = result.Id }, result);
    }

    [HttpDelete("recipients/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRecipient(string id)
    {
        var command = new RemoveEmailRecipientCommand(id);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Recipient not found")
                return NotFound();

            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }
}

// Request DTOs
public record UpdateTimerRequest(int IntervalHours);

public record UpdateSettingsRequest(
    string MinimumSeverityForAlert,
    int MaxConcurrentScans,
    int RetentionDays
);

public record CreateAzureConnectionRequest(
    string Name,
    string TenantId,
    string SubscriptionId,
    string ClientId,
    string ClientSecret
);

public record CreateAdoConnectionRequest(
    string Name,
    string Organization,
    string Project,
    string PatToken
);

public record AddRecipientRequest(
    string Email,
    string Name,
    string? MinimumSeverity
);
