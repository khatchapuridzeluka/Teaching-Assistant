using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("gradings/{gradingId:guid}/report")]
[Produces("application/json")]
public class GradingReportsController : ControllerBase
{
    private readonly IGradingReportService _reportService;

    public GradingReportsController(IGradingReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Returns the teacher's report on an AI draft: the edited score, review and mistakes, with each mistake's decision (kept, edited, rejected); the AI draft itself is never changed.
    /// </summary>
    /// <response code="200">Report retrieved successfully</response>
    /// <response code="204">No report yet (the draft has not been edited), or no draft with this ID</response>
    [HttpGet]
    [ProducesResponseType(typeof(GradingReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<GradingReportResponse>> GetAsync(Guid gradingId, CancellationToken cancellationToken)
    {
        var report = await _reportService.GetAsync(gradingId, cancellationToken);
        if (report is null)
        {
            return NoContent();
        }

        return Ok(report);
    }

    /// <summary>
    /// Saves the teacher's edits to an AI draft as a report in review (created from the draft on first save); saving an approved report reopens it.
    /// </summary>
    /// <response code="200">Report saved; returns it</response>
    /// <response code="400">Draft discarded, negative score or bonus, empty review, empty kept mistake, or a mistake that is not part of the draft</response>
    /// <response code="404">No draft with this ID</response>
    [HttpPut]
    [ProducesResponseType(typeof(GradingReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradingReportResponse>> SaveAsync(
        Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken)
    {
        var report = await _reportService.SaveAsync(gradingId, request, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        return Ok(report);
    }

    /// <summary>
    /// Saves the teacher's edits and approves the report as final; nothing is sent to Classroom or the student.
    /// </summary>
    /// <response code="200">Report approved; returns it</response>
    /// <response code="400">Draft discarded, negative score or bonus, empty review, empty kept mistake, or a mistake that is not part of the draft</response>
    /// <response code="404">No draft with this ID</response>
    [HttpPost("approve")]
    [ProducesResponseType(typeof(GradingReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradingReportResponse>> ApproveAsync(
        Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken)
    {
        var report = await _reportService.ApproveAsync(gradingId, request, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        return Ok(report);
    }
}
