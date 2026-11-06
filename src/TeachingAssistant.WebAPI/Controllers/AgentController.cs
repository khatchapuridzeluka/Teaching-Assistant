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

    public AgentController(IAgentChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Sends the teacher's message (with the conversation so far) to the assistant and returns its whole reply.
    /// </summary>
    /// <response code="200">The assistant's reply (Markdown)</response>
    /// <response code="400">Invalid conversation (empty, or not ending with the teacher's message)</response>
    /// <response code="503">Model unreachable or returned an error</response>
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _chatService.ReplyAsync(request, cancellationToken));
    }
}
