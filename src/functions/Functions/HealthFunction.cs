using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Chivato.Functions.Functions;

/// <summary>
/// Health check endpoint for container orchestration and monitoring
/// </summary>
public class HealthFunction
{
    private readonly ILogger<HealthFunction> _logger;

    public HealthFunction(ILogger<HealthFunction> logger)
    {
        _logger = logger;
    }

    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        _logger.LogDebug("Health check requested");

        return new OkObjectResult(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    [Function("HealthReady")]
    public IActionResult Ready(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")] HttpRequest req)
    {
        // Add more sophisticated readiness checks here if needed
        // e.g., check database connectivity, external services, etc.

        return new OkObjectResult(new
        {
            status = "ready",
            timestamp = DateTime.UtcNow
        });
    }

    [Function("HealthLive")]
    public IActionResult Live(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/live")] HttpRequest req)
    {
        return new OkObjectResult(new
        {
            status = "alive",
            timestamp = DateTime.UtcNow
        });
    }
}
