using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobService;

    public JobsController(IJobService jobService)
    {
        _jobService = jobService;
    }

    /// <summary>
    /// Queues an AI grading of a stored version and returns the job at once; the worker grades it in the background and saves the draft. Quick checks (version exists, instructions stored, something gradable) run before queuing; if the version is already queued or being graded, that job is returned.
    /// </summary>
    /// <response code="202">Grading queued (or already queued); follow it with GET /jobs/{jobId}</response>
    /// <response code="400">Nothing to grade, instructions not stored yet (fetch first), or no instructions the model can read</response>
    /// <response code="404">No stored version with this ID</response>
    [HttpPost("versions/{versionId:guid}/grade/queue")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var job = await _jobService.QueueGradeVersionAsync(versionId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Accepted($"/jobs/{job.Id}", job);
    }

    /// <summary>
    /// Returns a background job: its status, progress, error and, for a finished grading, the draft it produced.
    /// </summary>
    /// <response code="200">Job retrieved successfully</response>
    /// <response code="404">No job with this ID</response>
    [HttpGet("jobs/{jobId:guid}")]
    public async Task<ActionResult<JobResponse>> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    /// <summary>
    /// Returns an assignment's recent background jobs, newest first, so the page can show what is queued, running or failed.
    /// </summary>
    /// <response code="200">Jobs retrieved successfully; empty when there are none</response>
    [HttpGet("courses/{courseId}/assignments/{assignmentId}/jobs")]
    public async Task<ActionResult<IReadOnlyList<JobResponse>>> GetByAssignmentAsync(
        string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        return Ok(await _jobService.GetByAssignmentAsync(assignmentId, cancellationToken));
    }

    /// <summary>
    /// Cancels a job: a queued one at once, a running one at the worker's next check.
    /// </summary>
    /// <response code="200">Cancel recorded; returns the job</response>
    /// <response code="404">No job with this ID</response>
    [HttpPost("jobs/{jobId:guid}/cancel")]
    public async Task<ActionResult<JobResponse>> CancelAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobService.CancelAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }
}
