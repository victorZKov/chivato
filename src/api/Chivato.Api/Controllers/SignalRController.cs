using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR.Management;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SignalRController : ControllerBase
{
    private readonly string? _connectionString;
    private readonly ILogger<SignalRController> _logger;
    private readonly ServiceManager? _serviceManager;
    private const string HubName = "chivato";

    public SignalRController(
        IConfiguration configuration,
        ILogger<SignalRController> logger)
    {
        _connectionString = configuration["AzureSignalRConnectionString"];
        _logger = logger;

        if (!string.IsNullOrEmpty(_connectionString))
        {
            _serviceManager = new ServiceManagerBuilder()
                .WithOptions(opt =>
                {
                    opt.ConnectionString = _connectionString;
                    opt.ServiceTransportType = ServiceTransportType.Transient;
                })
                .BuildServiceManager();
        }
    }

    /// <summary>
    /// Negotiate SignalR connection for client
    /// </summary>
    [HttpPost("negotiate")]
    public async Task<IActionResult> Negotiate([FromQuery] string? userId = null)
    {
        if (string.IsNullOrEmpty(_connectionString) || _serviceManager == null)
        {
            return BadRequest(new { error = "SignalR not configured" });
        }

        try
        {
            // Get the hub context and generate negotiation response
            var hubContext = await _serviceManager.CreateHubContextAsync(HubName, default);
            var negotiateResponse = await hubContext.NegotiateAsync(new NegotiationOptions 
            { 
                UserId = userId 
            });

            return Ok(new
            {
                url = negotiateResponse.Url,
                accessToken = negotiateResponse.AccessToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to negotiate SignalR connection");
            return StatusCode(500, new { error = "Failed to negotiate connection" });
        }
    }

    /// <summary>
    /// Join a SignalR group (typically tenant group)
    /// </summary>
    [HttpPost("groups/join")]
    public async Task<IActionResult> JoinGroup([FromBody] GroupRequest request)
    {
        if (string.IsNullOrEmpty(_connectionString) || _serviceManager == null)
        {
            return BadRequest(new { error = "SignalR not configured" });
        }

        if (string.IsNullOrEmpty(request.ConnectionId) || string.IsNullOrEmpty(request.GroupName))
        {
            return BadRequest(new { error = "ConnectionId and GroupName are required" });
        }

        try
        {
            var hubContext = await _serviceManager.CreateHubContextAsync(HubName, default);
            await hubContext.Groups.AddToGroupAsync(request.ConnectionId, request.GroupName);

            _logger.LogInformation("Connection {ConnectionId} joined group {GroupName}",
                request.ConnectionId, request.GroupName);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join group {GroupName}", request.GroupName);
            return StatusCode(500, new { error = "Failed to join group" });
        }
    }

    /// <summary>
    /// Leave a SignalR group
    /// </summary>
    [HttpPost("groups/leave")]
    public async Task<IActionResult> LeaveGroup([FromBody] GroupRequest request)
    {
        if (string.IsNullOrEmpty(_connectionString) || _serviceManager == null)
        {
            return BadRequest(new { error = "SignalR not configured" });
        }

        if (string.IsNullOrEmpty(request.ConnectionId) || string.IsNullOrEmpty(request.GroupName))
        {
            return BadRequest(new { error = "ConnectionId and GroupName are required" });
        }

        try
        {
            var hubContext = await _serviceManager.CreateHubContextAsync(HubName, default);
            await hubContext.Groups.RemoveFromGroupAsync(request.ConnectionId, request.GroupName);

            _logger.LogInformation("Connection {ConnectionId} left group {GroupName}",
                request.ConnectionId, request.GroupName);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave group {GroupName}", request.GroupName);
            return StatusCode(500, new { error = "Failed to leave group" });
        }
    }

    /// <summary>
    /// Check if SignalR is configured and available
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            configured = !string.IsNullOrEmpty(_connectionString),
            hubName = HubName
        });
    }
}

public class GroupRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
}
