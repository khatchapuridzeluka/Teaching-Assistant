using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class GradingController : ControllerBase
{
    private readonly IGradingService _gradingService;
    private readonly IGradingDraftService _draftService;
    private readonly IGradingExportService _exportService;

    public GradingController(IGradingService gradingService, IGradingDraftService draftService, IGradingExportService exportService)
    {
        _gradingService = gradingService;
        _draftService = draftService;
        _exportService = exportService;
    }

    /// <summary>
    /// Sends a stored version and its assignment's stored instructions to the model, saves the answer as a new draft and returns it; can take a minute.
    /// </summary>
    /// <response code="200">The saved draft, with the instructions version used and the files that were sent and skipped</response>
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

    /// <summary>
    /// Returns the models a version can be tried with and the one that grades drafts now.
    /// </summary>
    /// <response code="200">The models</response>
    [HttpGet("grading-models")]
    public ActionResult<GradingModelsResponse> GetModels()
    {
        return Ok(_gradingService.GetModels());
    }

    /// <summary>
    /// Grades a stored version once with the chosen model, exactly like a draft, and returns the answer without saving it; can take a minute.
    /// </summary>
    /// <response code="200">The grading, shown like a draft but not stored</response>
    /// <response code="400">Unknown model, nothing to grade, or the instructions are not stored yet</response>
    /// <response code="404">No stored version with this ID</response>
    /// <response code="503">Model unreachable, returned an error, or declined</response>
    [HttpPost("versions/{versionId:guid}/try")]
    public async Task<ActionResult<GradingResponse>> TryModelAsync(Guid versionId, TryModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _gradingService.TryModelAsync(versionId, request.Model, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns the current draft of a version: its newest saved grading that was not discarded.
    /// </summary>
    /// <response code="200">The current draft</response>
    /// <response code="204">The version has no draft yet</response>
    [HttpGet("versions/{versionId:guid}/review")]
    [ProducesResponseType(typeof(GradingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<GradingResponse>> GetCurrentAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var current = await _draftService.GetCurrentAsync(versionId, cancellationToken);
        if (current is null)
        {
            return NoContent();
        }

        return Ok(current);
    }

    /// <summary>
    /// Returns every saved grading of a version, drafts and discarded, newest first.
    /// </summary>
    /// <response code="200">Gradings retrieved successfully; empty when the version was never graded</response>
    [HttpGet("versions/{versionId:guid}/gradings")]
    public async Task<ActionResult<IReadOnlyList<GradingResponse>>> GetHistoryAsync(Guid versionId, CancellationToken cancellationToken)
    {
        return Ok(await _draftService.GetHistoryAsync(versionId, cancellationToken));
    }

    /// <summary>
    /// Returns a course's saved gradings, newest first, with assignment, student, version, score and mistake counts; optionally one assignment's or one student's only, and by default only the current draft of each version.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="assignmentId">Only this assignment's gradings, when given.</param>
    /// <param name="studentId">Only this student's gradings (their Google user ID), when given; combines with assignmentId.</param>
    /// <param name="all">False (default): only the newest draft of each version; true: also earlier drafts and discarded ones.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">Gradings retrieved successfully; empty when nothing was graded</response>
    [HttpGet("courses/{courseId}/gradings")]
    public async Task<ActionResult<IReadOnlyList<AssignmentGradingResponse>>> GetByCourseAsync(
        string courseId, [FromQuery] string? assignmentId, [FromQuery] string? studentId, [FromQuery] bool all = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _draftService.GetByCourseAsync(courseId, assignmentId, studentId, all, cancellationToken));
    }

    /// <summary>
    /// Returns every saved grading of an assignment's stored versions (drafts, earlier drafts and discarded), newest first, with the student, score and mistake counts.
    /// </summary>
    /// <response code="200">Gradings retrieved successfully; empty when nothing was graded</response>
    [HttpGet("courses/{courseId}/assignments/{assignmentId}/gradings")]
    public async Task<ActionResult<IReadOnlyList<AssignmentGradingResponse>>> GetByAssignmentAsync(
        string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        return Ok(await _draftService.GetByCourseAsync(courseId, assignmentId, studentId: null, includeAll: true, cancellationToken));
    }

    /// <summary>
    /// Downloads one grading as a PDF report for the student: score, the review, mistakes and strengths, taken from the teacher's report when one exists and otherwise from the AI draft, with a banner saying whether it is approved; generated on request, never stored or sent anywhere.
    /// </summary>
    /// <response code="200">The PDF file</response>
    /// <response code="404">No grading with this ID, or its submission is no longer stored</response>
    [HttpGet("gradings/{gradingId:guid}/export/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPdfAsync(Guid gradingId, CancellationToken cancellationToken)
    {
        var file = await _exportService.ExportPdfAsync(gradingId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Discards a draft with why (optional body: reason and note); it stays in the version's history and is no longer its current draft.
    /// </summary>
    /// <response code="200">The discarded grading</response>
    /// <response code="400">The reason is Superseded or the note is too long</response>
    /// <response code="404">No grading with this ID</response>
    [HttpPost("gradings/{gradingId:guid}/discard")]
    public async Task<ActionResult<GradingResponse>> DiscardAsync(
        Guid gradingId, [FromBody] DiscardGradingRequest? request, CancellationToken cancellationToken)
    {
        var grading = await _draftService.DiscardAsync(gradingId, request, cancellationToken);
        if (grading is null)
        {
            return NotFound();
        }

        return Ok(grading);
    }
}
