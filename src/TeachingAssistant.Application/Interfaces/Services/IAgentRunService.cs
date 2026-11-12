using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IAgentRunService
{
    Task<AgentRunResponse?> StartAsync(StartAgentRunRequest request, CancellationToken cancellationToken = default);

    Task<AgentRunResponse?> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<AgentConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<AgentRunResponse?> CancelAsync(Guid runId, CancellationToken cancellationToken = default);
}
