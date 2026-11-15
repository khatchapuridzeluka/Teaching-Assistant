using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("policy-rules")]
[Produces("application/json")]
public class PolicyRulesController : ControllerBase
{
    private readonly IGradingGuidanceService _guidanceService;

    public PolicyRulesController(IGradingGuidanceService guidanceService)
    {
        _guidanceService = guidanceService;
    }

    /// <summary>
    /// Adds a grading rule after the existing ones; every course's next grading uses it.
    /// </summary>
    /// <response code="201">Rule added</response>
    /// <response code="400">A field is empty or too long, or a deduction has no valid points</response>
    [HttpPost]
    [ProducesResponseType(typeof(PolicyRuleResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PolicyRuleResponse>> AddAsync(PolicyRuleRequest request, CancellationToken cancellationToken)
    {
        var added = await _guidanceService.AddRuleAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, added);
    }

    /// <summary>
    /// Changes a grading rule; every course's next grading uses the new version.
    /// </summary>
    /// <response code="200">Rule updated</response>
    /// <response code="400">A field is empty or too long, or a deduction has no valid points</response>
    /// <response code="404">No rule with this ID, or it is deleted</response>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PolicyRuleResponse>> UpdateAsync(Guid id, PolicyRuleRequest request, CancellationToken cancellationToken)
    {
        var updated = await _guidanceService.UpdateRuleAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    /// <summary>
    /// Deletes a grading rule (it is only marked deleted); every course's next grading leaves it out.
    /// </summary>
    /// <response code="204">Rule deleted</response>
    /// <response code="404">No rule with this ID, or it is already deleted</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _guidanceService.DeleteRuleAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }
}
