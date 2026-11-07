using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses/{courseId}/assignments/{assignmentId}/instructions")]
public class InstructionsController : ControllerBase
{
    private readonly IInstructionsService _instructionsService;

    public InstructionsController(IInstructionsService instructionsService)
    {
        _instructionsService = instructionsService;
    }

    /// <summary>
    /// Returns the newest stored instructions of an assignment (task statement and materials), as grading sends them.
    /// </summary>
    /// <response code="200">Stored instructions retrieved successfully</response>
    /// <response code="204">Nothing stored yet; fetch from Classroom first</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StoredInstructionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<StoredInstructionsResponse>> GetAsync(string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        var instructions = await _instructionsService.GetLatestAsync(assignmentId, cancellationToken);
        if (instructions is null)
        {
            // Not an error: the assignment simply has not been fetched. A 404 would look like a missing endpoint.
            return NoContent();
        }

        return Ok(instructions);
    }

    /// <summary>
    /// Returns a stored instructions file: shown in the browser (PDF, images) or, with download=true, saved under its name.
    /// </summary>
    /// <response code="200">The file's content with its type</response>
    /// <response code="404">No stored instructions file with this ID in this assignment</response>
    [HttpGet("files/{contentId:guid}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFileAsync(
        string courseId, string assignmentId, Guid contentId, bool download, CancellationToken cancellationToken)
    {
        var file = await _instructionsService.OpenFileAsync(assignmentId, contentId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return download
            ? File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true)
            : File(file.Content, file.ContentType, enableRangeProcessing: true);
    }
}
