using Microsoft.AspNetCore.Mvc;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns "ok" when the API is running. Does not check the database, the model or any external service.
    /// </summary>
    /// <response code="200">API is running</response>
    [HttpGet]
    [Produces("text/plain")]
    public ActionResult<string> Get()
    {
        return Ok("ok");
    }
}
