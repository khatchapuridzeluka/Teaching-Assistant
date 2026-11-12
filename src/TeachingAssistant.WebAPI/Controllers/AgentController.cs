using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("agent")]
[Produces("application/json")]
public class AgentController : ControllerBase
{
    private readonly IAgentChatService _chatService;
    private readonly IAgentRunService _runService;

    public AgentController(IAgentChatService chatService, IAgentRunService runService)
    {
        _chatService = chatService;
        _runService = runService;
    }

    /// <summary>
    /// Sends the teacher's message (with the conversation so far) to the assistant and returns its whole reply.
    /// </summary>
    /// <response code="200">The assistant's reply (Markdown)</response>
    /// <response code="400">Invalid conversation: not ending with the teacher's message, or that message empty or over 8,000 characters</response>
    /// <response code="503">Model unreachable or returned an error</response>
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _chatService.ReplyAsync(request, cancellationToken));
    }

    /// <summary>
    /// Stores the teacher's message, queues a background agent run for it and returns the run at once; without a conversation ID a new conversation is started.
    /// </summary>
    /// <response code="202">Run queued; follow it with GET /agent/runs/{runId}</response>
    /// <response code="400">Message empty or over 8,000 characters, or the conversation's previous run has not finished</response>
    /// <response code="404">No conversation with this ID</response>
    [HttpPost("runs")]
    [ProducesResponseType(typeof(AgentRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentRunResponse>> StartRunAsync(StartAgentRunRequest request, CancellationToken cancellationToken)
    {
        var run = await _runService.StartAsync(request, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        return Accepted($"/agent/runs/{run.Id}", run);
    }

    /// <summary>
    /// Returns an agent run with its status, its final reply or error, and its messages so far (tool calls and tool results included) in order.
    /// </summary>
    /// <response code="200">Run retrieved successfully</response>
    /// <response code="404">No run with this ID</response>
    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<AgentRunResponse>> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _runService.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        return Ok(run);
    }

    /// <summary>
    /// Cancels an agent run: a waiting run stops at once, a running one within about a minute; jobs it already started keep running.
    /// </summary>
    /// <response code="200">Run canceled (or already finished)</response>
    /// <response code="404">No run with this ID</response>
    [HttpPost("runs/{runId:guid}/cancel")]
    public async Task<ActionResult<AgentRunResponse>> CancelRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _runService.CancelAsync(runId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        return Ok(run);
    }

    /// <summary>
    /// Returns a stored conversation: all its messages in order and the ID of the run still in progress, if any.
    /// </summary>
    /// <response code="200">Conversation retrieved successfully</response>
    /// <response code="404">No conversation with this ID</response>
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<AgentConversationResponse>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await _runService.GetConversationAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(conversation);
    }
}
