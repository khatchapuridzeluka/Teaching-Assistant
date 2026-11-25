using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class AssignmentTestsController : ControllerBase
{
    private readonly ITestDesignService _testDesignService;
    private readonly ITestRunService _testRunService;
    private readonly IJobService _jobService;

    public AssignmentTestsController(ITestDesignService testDesignService, ITestRunService testRunService, IJobService jobService)
    {
        _testDesignService = testDesignService;
        _testRunService = testRunService;
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

    /// <summary>
    /// Saves your edits to a test set: a draft is changed in place, an approved one becomes a new draft version. Requirements and cases are renumbered in the order given.
    /// </summary>
    /// <response code="200">Saved; returns the draft that holds the edits</response>
    /// <response code="400">A case without title or steps, a requirement without text, no cases at all, or a newer version exists</response>
    /// <response code="404">No test set with this ID for this assignment</response>
    [HttpPut("courses/{courseId}/assignments/{assignmentId}/tests/{testSetId:guid}")]
    [ProducesResponseType(typeof(TestSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestSetResponse>> SaveAsync(
        string courseId, string assignmentId, Guid testSetId, SaveTestSetRequest request, CancellationToken cancellationToken)
    {
        var testSet = await _testDesignService.SaveAsync(assignmentId, testSetId, request, cancellationToken);
        if (testSet is null)
        {
            return NotFound();
        }

        return Ok(testSet);
    }

    /// <summary>
    /// Approves the newest version of the assignment's tests; from then on it is the version gradings use.
    /// </summary>
    /// <response code="200">Approved (or already approved); returns the test set</response>
    /// <response code="400">A newer version exists</response>
    /// <response code="404">No test set with this ID for this assignment</response>
    [HttpPost("courses/{courseId}/assignments/{assignmentId}/tests/{testSetId:guid}/approve")]
    [ProducesResponseType(typeof(TestSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestSetResponse>> ApproveAsync(
        string courseId, string assignmentId, Guid testSetId, CancellationToken cancellationToken)
    {
        var testSet = await _testDesignService.ApproveAsync(assignmentId, testSetId, cancellationToken);
        if (testSet is null)
        {
            return NotFound();
        }

        return Ok(testSet);
    }

    /// <summary>
    /// Queues one paid model call that writes the JUnit test code for an approved class-design test set and compiles it in the sandbox; it also runs on its own when you approve.
    /// </summary>
    /// <response code="202">Code generation queued (or the running one is returned); follow it with GET /jobs/{jobId}</response>
    /// <response code="400">The test set is not approved, is console style, or a newer version exists</response>
    /// <response code="404">No test set with this ID for this assignment</response>
    [HttpPost("courses/{courseId}/assignments/{assignmentId}/tests/{testSetId:guid}/code/generate")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> GenerateCodeAsync(
        string courseId, string assignmentId, Guid testSetId, CancellationToken cancellationToken)
    {
        var job = await _testDesignService.QueueCodeAsync(assignmentId, testSetId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Accepted($"/jobs/{job.Id}", job);
    }

    /// <summary>
    /// Returns the newest test run of each student's version for this test set: per case passed, failed or not implemented, the adapter and any compiler output.
    /// </summary>
    /// <response code="200">Runs retrieved successfully (empty when nothing has run yet)</response>
    /// <response code="404">No test set with this ID for this assignment</response>
    [HttpGet("courses/{courseId}/assignments/{assignmentId}/tests/{testSetId:guid}/runs")]
    [ProducesResponseType(typeof(IReadOnlyList<TestRunResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TestRunResponse>>> GetRunsAsync(
        string courseId, string assignmentId, Guid testSetId, CancellationToken cancellationToken)
    {
        var runs = await _testRunService.GetLatestAsync(assignmentId, testSetId, cancellationToken);
        if (runs is null)
        {
            return NotFound();
        }

        return Ok(runs);
    }

    /// <summary>
    /// Queues a test run of every student's latest stored version: each compiles the student's code, writes or reuses their adapter (one small paid call per new version) and runs the test code in the sandbox.
    /// </summary>
    /// <response code="202">Runs queued; follow them with the assignment's jobs</response>
    /// <response code="400">The test set is not approved, has no test code, or its code does not compile</response>
    /// <response code="404">No test set with this ID for this assignment</response>
    [HttpPost("courses/{courseId}/assignments/{assignmentId}/tests/{testSetId:guid}/runs")]
    [ProducesResponseType(typeof(RunTestsResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunTestsResponse>> RunAsync(
        string courseId, string assignmentId, Guid testSetId, CancellationToken cancellationToken)
    {
        var queued = await _testRunService.QueueAllAsync(assignmentId, testSetId, cancellationToken);
        if (queued is null)
        {
            return NotFound();
        }

        return Accepted(queued);
    }

    /// <summary>
    /// Queues a test run of one student's version: it compiles the student's code, writes or reuses their adapter (one small paid call when there is none yet) and runs the test code in the sandbox.
    /// </summary>
    /// <response code="202">Run queued (or the running one is returned); follow it with GET /jobs/{jobId}</response>
    /// <response code="400">The test set is not approved, has no test code, or its code does not compile</response>
    /// <response code="404">No test set or version with these IDs for this assignment</response>
    [HttpPost("courses/{courseId}/assignments/{assignmentId}/tests/{testSetId:guid}/runs/{versionId:guid}")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> RunOneAsync(
        string courseId, string assignmentId, Guid testSetId, Guid versionId, CancellationToken cancellationToken)
    {
        var job = await _testRunService.QueueOneAsync(assignmentId, testSetId, versionId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Accepted($"/jobs/{job.Id}", job);
    }
}
