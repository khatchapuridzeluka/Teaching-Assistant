using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.WebAPI.Contracts;

namespace TeachingAssistant.WebAPI.Controllers;

/// <summary>
/// Connectivity check for the LLM used for grading.
/// </summary>
[ApiController]
[Route("health/model")]
[Produces("application/json")]
public class ModelHealthController : ControllerBase
{
    /// <summary>
    /// Sends a tiny test prompt to the configured model and reports whether it answered.
    /// </summary>
    /// <remarks>
    /// Its job is to verify the whole path to the model before any real grading runs:
    /// API key configured, model ID valid, network reachable, structured response parsed.
    /// It costs one very small request, so call it on demand, not as a frequent probe
    /// (use <c>GET /health</c> for that).
    ///
    /// Not implemented yet: the controller will delegate to an Application-layer service
    /// (backed by the Anthropic client in Infrastructure) and currently returns 501.
    /// </remarks>
    /// <param name="cancellationToken">Aborts the model call if the client disconnects.</param>
    /// <returns>A <see cref="ModelHealthResponse"/> with the model ID, latency and the model's reply.</returns>
    /// <response code="200">The model answered the test prompt.</response>
    /// <response code="501">The model check is not implemented yet.</response>
    /// <response code="503">The model could not be reached or returned an error (bad key, unknown model, timeout).</response>
    [HttpGet]
    [ProducesResponseType<ModelHealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType<ModelHealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // TODO: call the model health service and map its result to 200 / 503.
        IActionResult result = Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Model health check is not implemented yet.");
        return Task.FromResult(result);
    }
}
