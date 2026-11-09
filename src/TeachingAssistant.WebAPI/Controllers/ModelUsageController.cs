using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class ModelUsageController : ControllerBase
{
    private readonly IModelUsageService _usageService;

    public ModelUsageController(IModelUsageService usageService)
    {
        _usageService = usageService;
    }

    /// <summary>
    /// Returns the model calls of the last days (default 30): totals per purpose (calls, failures, input, output and cache tokens, average duration) and the 200 most recent calls; only metadata is recorded, never prompts or answers.
    /// </summary>
    /// <response code="200">Usage retrieved successfully</response>
    /// <response code="400">Days outside 1-365</response>
    [HttpGet("model-usage")]
    public async Task<ActionResult<ModelUsageResponse>> GetAsync([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        return Ok(await _usageService.GetAsync(days, cancellationToken));
    }
}
