using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Models;

public record AgentToolCall(string Id, string Name, string Arguments);

public record AgentEntry(
    AgentMessageRole Role, string Text, IReadOnlyList<AgentToolCall>? ToolCalls = null, string? ToolCallId = null, string? ToolName = null);

public record AgentReply(string Model, IReadOnlyList<AgentEntry> Entries, LanguageModelUsage? Usage = null)
{
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
