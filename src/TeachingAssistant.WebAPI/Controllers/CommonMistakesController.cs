using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class CommonMistakesController : ControllerBase
{
    private readonly IMistakeAnalysisService _analysisService;
    private readonly IJobService _jobService;

    public CommonMistakesController(IMistakeAnalysisService analysisService, IJobService jobService)
    {
        _analysisService = analysisService;
        _jobService = jobService;
    }

    /// <summary>
    /// Returns the latest common-mistakes analysis of an assignment: the AI's groups of mistakes with their explanation, your approved explanation, and students affected, points and severity counted in code.
    /// </summary>
    /// <response code="200">Analysis retrieved successfully</response>
    /// <response code="204">The assignment has not been analyzed yet</response>
    [HttpGet("courses/{courseId}/assignments/{assignmentId}/common-mistakes")]
    [ProducesResponseType(typeof(CommonMistakesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<CommonMistakesResponse>> GetAsync(string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        var analysis = await _analysisService.GetLatestAsync(courseId, assignmentId, cancellationToken);
        if (analysis is null)
        {
            return NoContent();
        }

        return Ok(analysis);
    }

    /// <summary>
    /// Queues an analysis of the assignment's current drafts (your report's version where one exists, rejected mistakes left out): the AI groups the mistakes by meaning, code validates and counts them; only mistake text, category, severity, points and location are sent, never names.
    /// </summary>
    /// <response code="202">Analysis queued (or the running one is returned); follow it with GET /jobs/{jobId}</response>
    [HttpPost("courses/{courseId}/assignments/{assignmentId}/common-mistakes/analyze")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<JobResponse>> AnalyzeAsync(string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        var job = await _jobService.QueueAnalyzeMistakesAsync(courseId, assignmentId, cancellationToken);
        return Accepted($"/jobs/{job.Id}", job);
    }

    /// <summary>
    /// Approves your explanation for a group of common mistakes (the AI's text stays as it was).
    /// </summary>
    /// <response code="200">Explanation approved; returns the group</response>
    /// <response code="400">The explanation is empty</response>
    /// <response code="404">No group with this ID</response>
    [HttpPost("mistake-clusters/{clusterId:guid}/approve")]
    [ProducesResponseType(typeof(MistakeClusterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MistakeClusterResponse>> ApproveAsync(
        Guid clusterId, ApproveExplanationRequest request, CancellationToken cancellationToken)
    {
        var cluster = await _analysisService.ApproveAsync(clusterId, request.Explanation, cancellationToken);
        if (cluster is null)
        {
            return NotFound();
        }

        return Ok(cluster);
    }
}
