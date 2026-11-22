using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeAgentChats : IAgentChatStore
{
    public List<AgentConversation> Conversations { get; } = [];

    public List<AgentRun> Runs { get; } = [];

    public List<AgentMessage> Messages { get; } = [];

    public int Saves { get; private set; }

    public Task<AgentConversation?> FindConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Conversations.FirstOrDefault(c => c.Id == conversationId));

    public Task<AgentRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.Id == runId));

    public Task<AgentRun?> FindUnfinishedRunAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.ConversationId == conversationId && !r.IsFinished));

    public Task<IReadOnlyList<AgentRun>> GetRunsWaitingOnAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentRun>>(Runs.Where(r => r.Status == AgentRunStatus.Waiting && r.WaitingJobIds.Contains(jobId)).ToList());

    public Task<IReadOnlyList<AgentRun>> GetUnfinishedRunsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentRun>>(Runs.Where(r => !r.IsFinished).ToList());

    public Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentMessage>>(Messages.Where(m => m.ConversationId == conversationId).OrderBy(m => m.Sequence).ToList());

    public Task<IReadOnlyList<AgentMessage>> GetRunMessagesAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentMessage>>(Messages.Where(m => m.RunId == runId).OrderBy(m => m.Sequence).ToList());

    public void Add(AgentConversation conversation) => Conversations.Add(conversation);

    public void Add(AgentRun run) => Runs.Add(run);

    public void Add(AgentMessage message) => Messages.Add(message);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
