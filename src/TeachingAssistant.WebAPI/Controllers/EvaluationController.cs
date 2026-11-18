using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("evaluation")]
[Produces("application/json")]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;

    public EvaluationController(IEvaluationService evaluationService)
    {
        _evaluationService = evaluationService;
    }

    /// <summary>
    /// Returns how the AI drafts compare with the approved reports made from them (scores, mistakes, strengths, per model), in all courses or one.
    /// </summary>
    /// <response code="200">Comparison computed; zero counts before any report is approved</response>
    [HttpGet]
    public async Task<ActionResult<EvaluationResponse>> GetAsync([FromQuery] string? courseId, CancellationToken cancellationToken)
    {
        return Ok(await _evaluationService.GetAsync(courseId, cancellationToken));
    }
}
