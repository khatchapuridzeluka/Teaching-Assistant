using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

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
    /// Returns the queue overview for the System page: counts (queued and running now, finished in the last 24 hours) and the 200 most recent jobs with the student and assignment they are about, newest first; filterable by status and kind of work.
    /// </summary>
    /// <response code="200">Overview retrieved successfully</response>
    [HttpGet("jobs")]
    public async Task<ActionResult<JobOverviewResponse>> GetOverviewAsync(
        [FromQuery] JobStatus? status, [FromQuery] JobType? type, [FromQuery] string? courseId, [FromQuery] string? assignmentId,
        CancellationToken cancellationToken)
    {
        return Ok(await _jobService.GetOverviewAsync(status, type, courseId, assignmentId, cancellationToken));
    }

    /// <summary>
    /// Queues a failed or canceled job again as a new job (the old one stays in the history); a grading is checked again first.
    /// </summary>
    /// <response code="202">Queued again (or an active job for the same work is returned)</response>
    /// <response code="400">The job is not failed or canceled, its type cannot be retried, or its version no longer exists</response>
    /// <response code="404">No job with this ID</response>
    [HttpPost("jobs/{jobId:guid}/retry")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> RetryAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobService.RetryAsync(jobId, cancellationToken);
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
