using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

// Everything about grading lives here. No shared route prefix: grading actions belong to different resources
// (a version today; later an assignment for "grade all" and a grading for edit and approve), so each action has its full route.
[ApiController]
[Produces("application/json")]
public class GradingController : ControllerBase
{
    private readonly IGradingService _gradingService;

    public GradingController(IGradingService gradingService)
    {
        _gradingService = gradingService;
    }

    /// <summary>
    /// Sends a stored version and its assignment's stored instructions to the model and returns the draft review (not saved); can take a minute.
    /// </summary>
    /// <response code="200">Draft review, with the instructions version used and the files that were sent and skipped</response>
    /// <response code="400">Nothing to grade, instructions not stored yet (fetch first), or no instructions the model can read</response>
    /// <response code="404">No stored version with this ID</response>
    /// <response code="503">Model unreachable, returned an error, or declined</response>
    [HttpPost("versions/{versionId:guid}/grade")]
    public async Task<ActionResult<GradingResponse>> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var result = await _gradingService.GradeVersionAsync(versionId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
