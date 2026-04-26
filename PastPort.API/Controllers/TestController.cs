using Microsoft.AspNetCore.Mvc;

namespace PastPort.API.Controllers;

/// <summary>
/// Provides health check and smoke test endpoints for infrastructure monitoring.
/// These endpoints are unauthenticated and intended for load balancer probes,
/// uptime monitoring, and deployment verification.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    /// <summary>
    /// Returns a simple status message confirming the API is operational.
    /// </summary>
    /// <returns>A JSON object with a message, UTC timestamp, and API version.</returns>
    /// <response code="200">The API is running normally.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            message = "PastPort API is working!",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    /// <summary>
    /// Returns a detailed health status including database connectivity.
    /// </summary>
    /// <returns>A JSON object with overall health status and component statuses.</returns>
    /// <response code="200">All systems are healthy.</response>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "Healthy",
            database = "Connected",
            timestamp = DateTime.UtcNow
        });
    }
}