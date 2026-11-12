namespace TeachingAssistant.Domain.Agents;

public enum AgentMessageRole
{
    User,

    Assistant,

    Tool,

    Notice,
}

public class AgentMessage
{
    private AgentMessage()
    {
    }

    internal AgentMessage(
        Guid conversationId, Guid runId, int sequence, AgentMessageRole role, string text, DateTimeOffset createdAt,
        string? toolCalls = null, string? toolCallId = null, string? toolName = null, Guid? jobId = null)
    {
        if (role == AgentMessageRole.Tool && (string.IsNullOrWhiteSpace(toolCallId) || string.IsNullOrWhiteSpace(toolName)))
        {
            throw new ArgumentException("A tool result needs the id and the name of the tool call it answers.", nameof(toolCallId));
        }

        if (role != AgentMessageRole.Assistant && toolCalls is not null)
        {
            throw new ArgumentException("Only the assistant can call tools.", nameof(toolCalls));
        }

        Id = Guid.CreateVersion7();
        ConversationId = conversationId;
        RunId = runId;
        Sequence = sequence;
        Role = role;
        Text = text.Trim();
        ToolCalls = toolCalls;
        ToolCallId = toolCallId;
        ToolName = toolName;
        JobId = jobId;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid RunId { get; private set; }

    public int Sequence { get; private set; }

    public AgentMessageRole Role { get; private set; }

    public string Text { get; private set; } = "";

    public string? ToolCalls { get; private set; }

    public string? ToolCallId { get; private set; }

    public string? ToolName { get; private set; }

    public Guid? JobId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
