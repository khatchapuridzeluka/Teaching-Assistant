using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses/{courseId}/assignments/{assignmentId}/submissions")]
[Produces("application/json")]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;
    private readonly ISubmissionFetchService _fetchService;

    public SubmissionsController(ISubmissionService submissionService, ISubmissionFetchService fetchService)
    {
        _submissionService = submissionService;
        _fetchService = fetchService;
    }

    /// <summary>
    /// Returns every student's submission to an assignment, sorted by student name, with attachments as metadata (no download).
    /// </summary>
    /// <response code="200">Submissions retrieved successfully</response>
    /// <response code="404">Course or assignment not found or not visible to the teacher</response>
    /// <response code="409">Google account not connected or authorization expired; call POST /auth/google/connect</response>
    /// <response code="502">Google Classroom returned an error or could not be reached</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubmissionResponse>>> GetAllAsync(
        string courseId, string assignmentId, CancellationToken cancellationToken) =>
        Ok(await _submissionService.GetSubmissionsAsync(courseId, assignmentId, cancellationToken));

    /// <summary>
    /// Downloads and stores every turned-in submission that is new or changed since the last fetch; unchanged ones are skipped.
    /// </summary>
    /// <response code="200">Fetch finished; the summary lists new submissions, new versions and problems</response>
    /// <response code="404">Course or assignment not found or not visible to the teacher</response>
    /// <response code="409">Google account not connected or authorization expired; call POST /auth/google/connect</response>
    /// <response code="502">Google Classroom or Drive returned an error or could not be reached</response>
    [HttpPost("fetch")]
    public async Task<ActionResult<FetchSummary>> FetchAsync(
        string courseId, string assignmentId, CancellationToken cancellationToken) =>
        Ok(await _fetchService.FetchAsync(courseId, assignmentId, cancellationToken));
}
