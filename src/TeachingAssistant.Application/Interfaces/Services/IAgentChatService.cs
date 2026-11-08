using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IAgentChatService
{
    Task<ChatResponse> ReplyAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
