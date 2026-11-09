using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Usage;

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
    /// Returns the model calls of the last days (default 30): a summary, tokens per day and the 200 most recent calls for all work or one purpose, plus the totals per purpose; only metadata is recorded, never prompts or answers.
    /// </summary>
    /// <response code="200">Usage retrieved successfully</response>
    /// <response code="400">Days outside 1-365</response>
    [HttpGet("model-usage")]
    public async Task<ActionResult<ModelUsageResponse>> GetAsync(
        [FromQuery] int days = 30, [FromQuery] ModelCallPurpose? purpose = null, CancellationToken cancellationToken = default)
    {
        return Ok(await _usageService.GetAsync(days, purpose, cancellationToken));
    }
}
