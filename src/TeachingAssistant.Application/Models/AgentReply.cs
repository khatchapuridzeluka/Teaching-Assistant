using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Models;

public record AgentToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }
}

public record AgentEntry
{
    public required AgentMessageRole Role { get; init; }
    public required string Text { get; init; }
    public IReadOnlyList<AgentToolCall>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
}

public record AgentReply
{
    public required string Model { get; init; }
    public required IReadOnlyList<AgentEntry> Entries { get; init; }
    public LanguageModelUsage? Usage { get; init; }

    /// <summary>
    /// The answer: the text of the final message when the model ended with one. When that final message is empty, the answer
    /// is the text the model wrote together with its last tool calls (a question asked in the same step as suggest_replies),
    /// since nothing came after it. Empty when the run stopped on a tool (for example to wait for jobs).
    /// </summary>
    public string Text
    {
        get
        {
            if (Entries is not [.., { Role: AgentMessageRole.Assistant, ToolCalls: null or [] } last])
            {
                return "";
            }

            if (last.Text.Length > 0)
            {
                return last.Text;
            }

            return Entries.LastOrDefault(e => e.Role == AgentMessageRole.Assistant && e.ToolCalls is { Count: > 0 })?.Text ?? "";
        }
    }
}
