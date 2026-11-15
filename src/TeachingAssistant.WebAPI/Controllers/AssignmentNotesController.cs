using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class AssignmentNotesController : ControllerBase
{
    private readonly IAssignmentNoteService _noteService;

    public AssignmentNotesController(IAssignmentNoteService noteService)
    {
        _noteService = noteService;
    }

    /// <summary>
    /// Returns the teacher's notes for one assignment, in order; the grader reads them only when it grades this assignment.
    /// </summary>
    /// <response code="200">Notes retrieved successfully; empty when the assignment has none</response>
    [HttpGet("courses/{courseId}/assignments/{assignmentId}/notes")]
    public async Task<ActionResult<IReadOnlyList<AssignmentNoteResponse>>> GetAllAsync(
        string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        return Ok(await _noteService.GetAsync(assignmentId, cancellationToken));
    }

    /// <summary>
    /// Adds a note to the assignment after its existing ones; the assignment's next grading uses it.
    /// </summary>
    /// <response code="201">Note added</response>
    /// <response code="400">Title or text is empty or too long</response>
    [HttpPost("courses/{courseId}/assignments/{assignmentId}/notes")]
    [ProducesResponseType(typeof(AssignmentNoteResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AssignmentNoteResponse>> AddAsync(
        string courseId, string assignmentId, AssignmentNoteRequest request, CancellationToken cancellationToken)
    {
        var added = await _noteService.AddAsync(courseId, assignmentId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, added);
    }

    /// <summary>
    /// Changes an assignment note's title and text; the assignment's next grading uses the new text.
    /// </summary>
    /// <response code="200">Note updated</response>
    /// <response code="400">Title or text is empty or too long</response>
    /// <response code="404">No note with this ID, or it is deleted</response>
    [HttpPut("assignment-notes/{id:guid}")]
    public async Task<ActionResult<AssignmentNoteResponse>> UpdateAsync(Guid id, AssignmentNoteRequest request, CancellationToken cancellationToken)
    {
        var updated = await _noteService.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    /// <summary>
    /// Deletes an assignment note (it is only marked deleted); the assignment's next grading leaves it out.
    /// </summary>
    /// <response code="204">Note deleted</response>
    /// <response code="404">No note with this ID, or it is already deleted</response>
    [HttpDelete("assignment-notes/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _noteService.DeleteAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }
}
