using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses/{courseId}/students")]
[Produces("application/json")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
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
}
