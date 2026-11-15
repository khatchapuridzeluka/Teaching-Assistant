using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses/{courseId}/grading-guidance")]
[Produces("application/json")]
public class GradingGuidanceController : ControllerBase
{
    private readonly IGradingGuidanceService _guidanceService;

    public GradingGuidanceController(IGradingGuidanceService guidanceService)
    {
        _guidanceService = guidanceService;
    }

    /// <summary>
    /// Returns what the course is graded with: the course's own guidance (none yet), the grader's built-in approach and the grading rules, each with the exact text the grader receives.
    /// </summary>
    /// <response code="200">Guidance retrieved successfully</response>
    [HttpGet]
    public async Task<ActionResult<GradingGuidanceResponse>> GetAsync(string courseId, CancellationToken cancellationToken)
    {
        return Ok(await _guidanceService.GetAsync(courseId, cancellationToken));
    }
}
