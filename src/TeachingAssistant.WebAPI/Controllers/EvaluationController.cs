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
    private readonly IEvaluationRunService _runService;

    public EvaluationController(IEvaluationService evaluationService, IEvaluationRunService runService)
    {
        _evaluationService = evaluationService;
        _runService = runService;
    }

    /// <summary>
    /// Returns how the AI drafts, or one model's side gradings (model), compare with the approved reports, in all courses or one.
    /// </summary>
    /// <response code="200">Comparison computed; zero counts before any report is approved or graded by the model</response>
    /// <response code="400">Unknown model</response>
    [HttpGet]
    public async Task<ActionResult<EvaluationResponse>> GetAsync(
        [FromQuery] string? courseId, [FromQuery] string? model, CancellationToken cancellationToken)
    {
        return Ok(await _evaluationService.GetAsync(courseId, model, cancellationToken));
    }

    /// <summary>
    /// Returns the models compared on the same homework against the approved reports, and the newest evaluation run's progress.
    /// </summary>
    /// <response code="200">Comparison computed; no rows before the first run</response>
    [HttpGet("models")]
    public async Task<ActionResult<ModelComparisonResponse>> GetModelsAsync([FromQuery] string? courseId, CancellationToken cancellationToken)
    {
        return Ok(await _runService.GetAsync(courseId, cancellationToken));
    }

    /// <summary>
    /// Starts an evaluation run: every approved report is graded again on the side by each chosen model, in the background; nothing becomes a draft.
    /// </summary>
    /// <response code="200">Run queued; the comparison with its progress</response>
    /// <response code="400">No model chosen, an unknown model, or no approved report yet</response>
    [HttpPost("runs")]
    public async Task<ActionResult<ModelComparisonResponse>> StartRunAsync(StartEvaluationRunRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _runService.StartAsync(request, cancellationToken));
    }
}
