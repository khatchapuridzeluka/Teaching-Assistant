using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
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

    /// <summary>
    /// Returns a student's profile photo from our stored copy (by their Google user ID); the browser revalidates it by its hash (ETag), so an unchanged photo is not sent again.
    /// </summary>
    /// <response code="200">The photo</response>
    /// <response code="304">The browser's copy is still current</response>
    /// <response code="404">The student has no stored photo (show initials instead)</response>
    [HttpGet("/students/{userId}/photo")]
    [Produces("image/jpeg", "image/png", "image/webp", "image/gif")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotoAsync(string userId, CancellationToken cancellationToken)
    {
        var photo = await _studentService.OpenPhotoAsync(userId, cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-cache";
        return File(photo.Content, photo.ContentType, lastModified: null, entityTag: new EntityTagHeaderValue($"\"{photo.Hash}\""));
    }
}
