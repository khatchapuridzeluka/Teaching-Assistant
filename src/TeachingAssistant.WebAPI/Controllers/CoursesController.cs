using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses")]
[Produces("application/json")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly ICourseSyncService _courseSyncService;

    public CoursesController(ICourseService courseService, ICourseSyncService courseSyncService)
    {
        _courseService = courseService;
        _courseSyncService = courseSyncService;
    }

    /// <summary>
    /// Copies the course's roster, assignments and every student's state and Classroom grades into the database; downloads no files and writes nothing to Classroom.
    /// </summary>
    /// <response code="200">Course synced; returns what changed</response>
    /// <response code="404">Course not found in Google Classroom</response>
    /// <response code="409">Google account not connected or authorization expired; call POST /auth/google/connect</response>
    /// <response code="502">Google Classroom returned an error or could not be reached</response>
    [HttpPost("{courseId}/sync")]
    public async Task<ActionResult<CourseSyncSummary>> SyncAsync(string courseId, CancellationToken cancellationToken)
    {
        return Ok(await _courseSyncService.SyncAsync(courseId, cancellationToken));
    }

    /// <summary>
    /// Returns all courses the teacher teaches in Google Classroom (active and archived), sorted by name.
    /// </summary>
    /// <response code="200">Courses retrieved successfully</response>
    /// <response code="409">Google account not connected or authorization expired; call POST /auth/google/connect</response>
    /// <response code="502">Google Classroom returned an error or could not be reached</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CourseResponse>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await _courseService.GetCoursesAsync(cancellationToken));
    }
}
