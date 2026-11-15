using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("grading-principles")]
[Produces("application/json")]
public class GradingPrinciplesController : ControllerBase
{
    private readonly IGradingGuidanceService _guidanceService;

    public GradingPrinciplesController(IGradingGuidanceService guidanceService)
    {
        _guidanceService = guidanceService;
    }

    /// <summary>
    /// Adds a built-in grading principle after the existing ones; every course's next grading uses it.
    /// </summary>
    /// <response code="201">Principle added</response>
    /// <response code="400">Title or text is empty or too long</response>
    [HttpPost]
    [ProducesResponseType(typeof(GradingPrincipleResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<GradingPrincipleResponse>> AddAsync(GradingPrincipleRequest request, CancellationToken cancellationToken)
    {
        var added = await _guidanceService.AddPrincipleAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, added);
    }

    /// <summary>
    /// Changes a built-in grading principle's title and text; every course's next grading uses the new text.
    /// </summary>
    /// <response code="200">Principle updated</response>
    /// <response code="400">Title or text is empty or too long</response>
    /// <response code="404">No principle with this ID, or it is deleted</response>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GradingPrincipleResponse>> UpdateAsync(
        Guid id, GradingPrincipleRequest request, CancellationToken cancellationToken)
    {
        var updated = await _guidanceService.UpdatePrincipleAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    /// <summary>
    /// Deletes a built-in grading principle (it is only marked deleted); every course's next grading leaves it out.
    /// </summary>
    /// <response code="204">Principle deleted</response>
    /// <response code="404">No principle with this ID, or it is already deleted</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _guidanceService.DeletePrincipleAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }
}
