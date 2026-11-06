using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// The teacher's chat with the assistant. No tools yet: it answers from the conversation alone.
/// </summary>
public interface IAgentChatService
{
    /// <summary>
    /// Returns the assistant's reply. Throws <see cref="Exceptions.InvalidRequestException"/> for an invalid conversation
    /// and <see cref="Exceptions.LanguageModelUnavailableException"/> when the model fails.
    /// </summary>
    Task<ChatResponse> ReplyAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
