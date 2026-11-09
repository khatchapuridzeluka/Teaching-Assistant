using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("courses/{courseId}/gradebook")]
[Produces("application/json")]
public class GradebookController : ControllerBase
{
    private readonly IGradebookService _gradebookService;

    public GradebookController(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    /// <summary>
    /// Returns the course gradebook: enrolled students × assignments with each cell's state, initial and final grade, per-assignment summaries and overall scores, from the grades synced from Classroom.
    /// </summary>
    /// <response code="200">Gradebook retrieved successfully; empty lists before the first sync</response>
    [HttpGet]
    public async Task<ActionResult<GradebookResponse>> GetAsync(string courseId, CancellationToken cancellationToken)
    {
        return Ok(await _gradebookService.GetAsync(courseId, cancellationToken));
    }

    /// <summary>
    /// Downloads the gradebook as a CSV file (initial and final grade per assignment, overall %), for the teacher's own records; nothing is written to Classroom.
    /// </summary>
    /// <response code="200">The CSV file</response>
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAsync(string courseId, CancellationToken cancellationToken)
    {
        var csv = await _gradebookService.ExportCsvAsync(courseId, cancellationToken);
        return File(csv, "text/csv; charset=utf-8", $"gradebook-{courseId}.csv");
    }
}
