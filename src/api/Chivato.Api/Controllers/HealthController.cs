using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    [HttpGet("ready")]
    public IActionResult GetReady()
    {
        return Ok(new { status = "ready" });
    }

    [HttpGet("live")]
    public IActionResult GetLive()
    {
        return Ok(new { status = "live" });
    }
}
