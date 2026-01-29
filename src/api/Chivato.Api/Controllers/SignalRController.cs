using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR.Management;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SignalRController : ControllerBase
{
    private readonly string? _connectionString;
    private readonly ILogger<SignalRController> _logger;
    private const string HubName = "chivato";

    public SignalRController(
        IConfiguration configuration,
        ILogger<SignalRController> logger)
    {
        _connectionString = configuration["AzureSignalRConnectionString"];
        _logger = logger;
    }

    /// <summary>
    /// Negotiate SignalR connection for client
    /// </summary>
    [HttpPost("negotiate")]
    public async Task<IActionResult> Negotiate([FromQuery] string? userId = null)
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            return BadRequest(new { error = "SignalR not configured" });
        }

        try
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(opt => opt.ConnectionString = _connectionString)
                .BuildServiceManager();

            var negotiationResponse = await serviceManager.CreateHubContextAsync(HubName, default);

            // Generate client access URL and token
            var url = $"{GetSignalREndpoint()}/client/?hub={HubName}";
            var accessToken = await GenerateAccessTokenAsync(serviceManager, userId);

            return Ok(new
            {
                url,
                accessToken
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
        if (string.IsNullOrEmpty(_connectionString))
        {
            return BadRequest(new { error = "SignalR not configured" });
        }

        if (string.IsNullOrEmpty(request.ConnectionId) || string.IsNullOrEmpty(request.GroupName))
        {
            return BadRequest(new { error = "ConnectionId and GroupName are required" });
        }

        try
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(opt => opt.ConnectionString = _connectionString)
                .BuildServiceManager();

            var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
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
        if (string.IsNullOrEmpty(_connectionString))
        {
            return BadRequest(new { error = "SignalR not configured" });
        }

        if (string.IsNullOrEmpty(request.ConnectionId) || string.IsNullOrEmpty(request.GroupName))
        {
            return BadRequest(new { error = "ConnectionId and GroupName are required" });
        }

        try
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(opt => opt.ConnectionString = _connectionString)
                .BuildServiceManager();

            var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
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

    private string GetSignalREndpoint()
    {
        if (string.IsNullOrEmpty(_connectionString))
            return string.Empty;

        // Parse endpoint from connection string
        var parts = _connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                return part.Substring("Endpoint=".Length).TrimEnd('/');
            }
        }
        return string.Empty;
    }

    private async Task<string> GenerateAccessTokenAsync(ServiceManager serviceManager, string? userId)
    {
        // For serverless mode, we need to generate a token
        // This is a simplified implementation - in production you'd use proper JWT generation
        var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // The actual token generation would depend on your authentication setup
        // For now, return a placeholder that indicates the client should connect
        return $"token-{userId ?? "anonymous"}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }
}

public class GroupRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
}
