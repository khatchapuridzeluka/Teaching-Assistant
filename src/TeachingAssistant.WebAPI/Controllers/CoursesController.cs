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

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    /// <summary>
    /// Returns all courses the teacher teaches in Google Classroom (active and archived), sorted by name.
    /// </summary>
    /// <response code="200">Courses retrieved successfully</response>
    /// <response code="409">Google account not connected or authorization expired; call POST /auth/google/connect</response>
    /// <response code="502">Google Classroom returned an error or could not be reached</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CourseResponse>>> GetAllAsync(CancellationToken cancellationToken) =>
        Ok(await _courseService.GetCoursesAsync(cancellationToken));
}
