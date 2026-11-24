using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class AssignmentTestsController : ControllerBase
{
    private readonly ITestDesignService _testDesignService;
    private readonly IJobService _jobService;

    public AssignmentTestsController(ITestDesignService testDesignService, IJobService jobService)
    {
        _testDesignService = testDesignService;
        _jobService = jobService;
    }

    /// <summary>
    /// Returns the assignment's latest test set: its requirements with how many cases check each, the neutral interface and the test cases.
    /// </summary>
    /// <response code="200">Test set retrieved successfully</response>
    /// <response code="204">The assignment has no test set yet</response>
    [HttpGet("courses/{courseId}/assignments/{assignmentId}/tests")]
    [ProducesResponseType(typeof(TestSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<TestSetResponse>> GetAsync(string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        var testSet = await _testDesignService.GetLatestAsync(assignmentId, cancellationToken);
        if (testSet is null)
        {
            return NoContent();
        }

        return Ok(testSet);
    }

    /// <summary>
    /// Queues one paid model call that designs test cases from the stored instructions, the teacher's settings and the optional reference solution; the result is a new draft version.
    /// </summary>
    /// <response code="202">Generation queued (or the running one is returned); follow it with GET /jobs/{jobId}</response>
    /// <response code="400">The settings are invalid, such as no kind of case chosen or a reference file that is not .java</response>
    [HttpPost("courses/{courseId}/assignments/{assignmentId}/tests/generate")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobResponse>> GenerateAsync(
        string courseId, string assignmentId, GenerateTestsRequest request, CancellationToken cancellationToken)
    {
        var job = await _jobService.QueueGenerateTestsAsync(courseId, assignmentId, request, cancellationToken);
        return Accepted($"/jobs/{job.Id}", job);
    }
}
