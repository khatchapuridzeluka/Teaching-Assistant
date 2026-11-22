using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Models;

public record AgentEntry
{
    public required AgentMessageRole Role { get; init; }
    public required string Text { get; init; }
    public IReadOnlyList<AgentToolCall>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
}
