using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Models;

public record AgentToolCall(string Id, string Name, string Arguments);

public record AgentEntry(
    AgentMessageRole Role, string Text, IReadOnlyList<AgentToolCall>? ToolCalls = null, string? ToolCallId = null, string? ToolName = null);

public record AgentReply(string Model, IReadOnlyList<AgentEntry> Entries, LanguageModelUsage? Usage = null)
{
    public string Text => Entries is [.., { Role: AgentMessageRole.Assistant, ToolCalls: null or [] } last] ? last.Text : "";
}
