using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Produces("application/json")]
public class ReviewQueueController : ControllerBase
{
    private readonly IReviewQueueService _reviewQueueService;

    public ReviewQueueController(IReviewQueueService reviewQueueService)
    {
        _reviewQueueService = reviewQueueService;
    }

    /// <summary>
    /// Returns what waits for the teacher across all courses: current AI drafts not yet decided (oldest first) and turned-in submissions whose latest version has no draft yet.
    /// </summary>
    /// <response code="200">Review queue retrieved successfully; both lists are empty when nothing waits</response>
    [HttpGet("review-queue")]
    public async Task<ActionResult<ReviewQueueResponse>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await _reviewQueueService.GetAsync(cancellationToken));
    }
}
