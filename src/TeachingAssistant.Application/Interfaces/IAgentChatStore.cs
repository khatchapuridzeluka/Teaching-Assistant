using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Interfaces;

public interface IAgentChatStore
{
    Task<AgentConversation?> FindConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<AgentRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<AgentRun?> FindUnfinishedRunAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentMessage>> GetRunMessagesAsync(Guid runId, CancellationToken cancellationToken = default);

    void Add(AgentConversation conversation);

    void Add(AgentRun run);

    void Add(AgentMessage message);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
