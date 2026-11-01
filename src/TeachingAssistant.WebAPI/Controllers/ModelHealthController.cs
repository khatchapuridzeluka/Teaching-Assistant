using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("health/model")]
[Produces("application/json")]
public class ModelHealthController : ControllerBase
{
    private readonly IModelHealthService _modelHealthService;

    public ModelHealthController(IModelHealthService modelHealthService)
    {
        _modelHealthService = modelHealthService;
    }

    /// <summary>
    /// Sends a small test prompt to the grading model and returns the model ID, latency and reply.
    /// </summary>
    /// <response code="200">Model answered the test prompt</response>
    /// <response code="503">Model unreachable or returned an error (bad key, unknown model, rate limit, network)</response>
    [HttpGet]
    public async Task<ActionResult<ModelHealthResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var result = await _modelHealthService.CheckAsync(cancellationToken);
        return result.Healthy ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }
}
