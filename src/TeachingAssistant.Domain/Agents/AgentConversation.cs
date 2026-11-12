namespace TeachingAssistant.Domain.Agents;

public class AgentConversation
{
    private AgentConversation()
    {
    }

    public AgentConversation(DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CreatedAt = createdAt.ToUniversalTime();
        LastActivityAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public int MessageCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public AgentMessage AddUserMessage(Guid runId, string text, DateTimeOffset at)
    {
        return Add(new AgentMessage(Id, runId, MessageCount, AgentMessageRole.User, text, at));
    }

    public AgentMessage AddAssistantMessage(Guid runId, string text, string? toolCalls, DateTimeOffset at)
    {
        return Add(new AgentMessage(Id, runId, MessageCount, AgentMessageRole.Assistant, text, at, toolCalls: toolCalls));
    }

    public AgentMessage AddToolResult(
        Guid runId, string toolCallId, string toolName, string text, Guid? jobId, DateTimeOffset at, string? links = null)
    {
        return Add(new AgentMessage(Id, runId, MessageCount, AgentMessageRole.Tool, text, at, toolCallId: toolCallId, toolName: toolName,
            jobId: jobId, links: links));
    }

    public AgentMessage AddNotice(Guid runId, string text, Guid? jobId, DateTimeOffset at, string? links = null)
    {
        return Add(new AgentMessage(Id, runId, MessageCount, AgentMessageRole.Notice, text, at, jobId: jobId, links: links));
    }

    private AgentMessage Add(AgentMessage message)
    {
        MessageCount++;
        LastActivityAt = message.CreatedAt;
        return message;
    }
}
