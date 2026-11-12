using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Infrastructure.Persistence;

public class AgentChatStore(AppDbContext db) : IAgentChatStore
{
    public Task<AgentConversation?> FindConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        db.AgentConversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

    public Task<AgentRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

    public Task<AgentRun?> FindUnfinishedRunAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        db.AgentRuns
            .Where(r => r.ConversationId == conversationId
                && (r.Status == AgentRunStatus.Queued || r.Status == AgentRunStatus.Running || r.Status == AgentRunStatus.Waiting))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        await db.AgentMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentMessage>> GetRunMessagesAsync(Guid runId, CancellationToken cancellationToken = default) =>
        await db.AgentMessages
            .Where(m => m.RunId == runId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);

    public void Add(AgentConversation conversation) => db.AgentConversations.Add(conversation);

    public void Add(AgentRun run) => db.AgentRuns.Add(run);

    public void Add(AgentMessage message) => db.AgentMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
