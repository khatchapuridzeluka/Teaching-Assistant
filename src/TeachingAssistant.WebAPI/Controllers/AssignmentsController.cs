using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses/{courseId}/assignments")]
[Produces("application/json")]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    /// <summary>
    /// Returns the published assignments of a Google Classroom course, newest first, including the task statement.
    /// </summary>
    /// <response code="200">Assignments retrieved successfully</response>
    /// <response code="404">Course not found or not visible to the teacher</response>
    /// <response code="409">Google account not connected or authorization expired; call POST /auth/google/connect</response>
    /// <response code="502">Google Classroom returned an error or could not be reached</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssignmentResponse>>> GetAllAsync(string courseId, CancellationToken cancellationToken)
    {
        return Ok(await _assignmentService.GetAssignmentsAsync(courseId, cancellationToken));
    }
}
