using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses/{courseId}/students")]
[Produces("application/json")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IStudentMistakeService _mistakeService;
    private readonly IStudentStrengthService _strengthService;
    private readonly IStudentAnalysisService _analysisService;
    private readonly IStudentOverviewService _overviewService;
    private readonly IJobService _jobService;

    public StudentsController(
        IStudentService studentService, IStudentMistakeService mistakeService, IStudentStrengthService strengthService,
        IStudentAnalysisService analysisService, IStudentOverviewService overviewService, IJobService jobService)
    {
        _studentService = studentService;
        _mistakeService = mistakeService;
        _strengthService = strengthService;
        _analysisService = analysisService;
        _overviewService = overviewService;
        _jobService = jobService;
    }

    /// <summary>
    /// Returns the course's students with their state, initial and final grade per assignment and overall score, from the database (filled by POST /courses/{courseId}/sync).
    /// </summary>
    /// <response code="200">Students retrieved successfully; empty lists before the first sync</response>
    [HttpGet]
    public async Task<ActionResult<CourseStudentsResponse>> GetAllAsync(string courseId, CancellationToken cancellationToken)
    {
        return Ok(await _studentService.GetCourseStudentsAsync(courseId, cancellationToken));
    }

    /// <summary>
    /// Returns a student's mistakes in the course (per graded assignment: your newest report when you saved one, even if a newer AI draft exists, otherwise the newest AI draft; each item and assignment says which (Draft, InReview, Approved)), newest assignment first, each labelled new or repeated (same category in an earlier assignment; labels always come from the full history), with breakdowns by category, severity, label and assignment; filterable by assignment, category, severity and label.
    /// </summary>
    /// <response code="200">Mistakes retrieved successfully; empty lists when the student has no approved report yet</response>
    [HttpGet("{userId}/mistakes")]
    public async Task<ActionResult<StudentMistakesResponse>> GetMistakesAsync(
        string courseId, string userId, [FromQuery] string? assignmentId, [FromQuery] MistakeCategory? category,
        [FromQuery] MistakeSeverity? severity, [FromQuery] MistakeLabel? label, CancellationToken cancellationToken)
    {
        var filter = new StudentMistakeFilter(assignmentId, category, severity, label);
        return Ok(await _mistakeService.GetAsync(courseId, userId, filter, cancellationToken));
    }

    /// <summary>
    /// Returns a student's strengths in the course (per graded assignment: your newest report when you saved one, even if a newer AI draft exists, otherwise the newest AI draft; each item and assignment says which (Draft, InReview, Approved)), newest assignment first, each labelled new, consistent (same category was a strength in an earlier assignment) or improved (same category was a mistake in an earlier assignment), with breakdowns by category and label and strengths vs. mistakes per assignment; filterable by assignment, category and label.
    /// </summary>
    /// <response code="200">Strengths retrieved successfully; empty lists when the student has no approved report with strengths yet</response>
    [HttpGet("{userId}/strengths")]
    public async Task<ActionResult<StudentStrengthsResponse>> GetStrengthsAsync(
        string courseId, string userId, [FromQuery] string? assignmentId, [FromQuery] MistakeCategory? category,
        [FromQuery] StrengthLabel? label, CancellationToken cancellationToken)
    {
        var filter = new StudentStrengthFilter(assignmentId, category, label);
        return Ok(await _strengthService.GetAsync(courseId, userId, filter, cancellationToken));
    }

    /// <summary>
    /// Returns the full overview of a student, counted in code from the same records as the mistakes and strengths pages: a score per graded assignment (your report's where you reviewed it, otherwise the AI draft's), mistakes and strengths per assignment and per category, and the top mistake and strength categories.
    /// </summary>
    /// <response code="200">Overview retrieved successfully; empty lists when nothing is graded yet</response>
    [HttpGet("{userId}/overview")]
    public async Task<ActionResult<StudentOverviewResponse>> GetOverviewAsync(string courseId, string userId, CancellationToken cancellationToken)
    {
        return Ok(await _overviewService.GetAsync(courseId, userId, cancellationToken));
    }

    /// <summary>
    /// Returns the latest AI analysis of the student's mistakes (feedback of at most 200 words and what to work on next) strengths (the strengths grouped into skills and an overview) or the full overview (an overall picture of at most 250 words and what to work on next), with what it was based on and whether reports changed since.
    /// </summary>
    /// <response code="200">Analysis retrieved successfully</response>
    /// <response code="204">The student has not been analyzed for this kind yet</response>
    [HttpGet("{userId}/analysis")]
    [ProducesResponseType(typeof(StudentAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<StudentAnalysisResponse>> GetAnalysisAsync(
        string courseId, string userId, [FromQuery] StudentAnalysisKind kind, CancellationToken cancellationToken)
    {
        var analysis = await _analysisService.GetLatestAsync(courseId, userId, kind, cancellationToken);
        if (analysis is null)
        {
            return NoContent();
        }

        return Ok(analysis);
    }

    /// <summary>
    /// Queues an AI analysis of the student from each graded assignment (your newest report when you saved one, otherwise the newest AI draft): Mistakes sends only the kept mistakes with their points and labels, Strengths sends only the kept strengths to be grouped into skills, Overview sends the scores with both mistakes and strengths for an overall picture; trends are counted in code and the student is never named.
    /// </summary>
    /// <response code="202">Analysis queued (or the running one is returned); follow it with GET /jobs/{jobId}</response>
    [HttpPost("{userId}/analysis")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<JobResponse>> AnalyzeAsync(
        string courseId, string userId, [FromQuery] StudentAnalysisKind kind, CancellationToken cancellationToken)
    {
        var job = await _jobService.QueueAnalyzeStudentAsync(courseId, userId, kind, cancellationToken);
        return Accepted($"/jobs/{job.Id}", job);
    }

    /// <summary>
    /// Returns a student's profile photo from our stored copy (by their Google user ID); the browser revalidates it by its hash (ETag), so an unchanged photo is not sent again.
    /// </summary>
    /// <response code="200">The photo</response>
    /// <response code="304">The browser's copy is still current</response>
    /// <response code="404">The student has no stored photo (show initials instead)</response>
    [HttpGet("/students/{userId}/photo")]
    [Produces("image/jpeg", "image/png", "image/webp", "image/gif")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotoAsync(string userId, CancellationToken cancellationToken)
    {
        var photo = await _studentService.OpenPhotoAsync(userId, cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-cache";
        return File(photo.Content, photo.ContentType, lastModified: null, entityTag: new EntityTagHeaderValue($"\"{photo.Hash}\""));
    }
}
