using Microsoft.AspNetCore.Mvc;

namespace TeachingAssistant.WebAPI.Controllers;

/// <summary>
/// Liveness check for the API process itself.
/// </summary>
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Reports that the API is up and able to handle requests.
    /// </summary>
    /// <remarks>
    /// Its only job is to answer quickly: it does not touch the database, the LLM,
    /// Google Classroom or the Docker sandboxes. Use it for "is the server running?"
    /// checks (local dev, Cloud Run health probes). To check the LLM connection,
    /// use <c>GET /health/model</c> instead.
    /// </remarks>
    /// <returns>The plain-text string <c>ok</c>.</returns>
    /// <response code="200">The API is running.</response>
    [HttpGet]
    [Produces("text/plain")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok("ok");
}
