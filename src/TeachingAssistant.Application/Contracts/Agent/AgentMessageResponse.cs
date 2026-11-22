using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Contracts;

public record AgentMessageResponse
{
    public required Guid Id { get; init; }
    public required Guid RunId { get; init; }
    public required int Sequence { get; init; }
    public required AgentMessageRole Role { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<string> ToolCalls { get; init; }
    public string? ToolName { get; init; }
    public Guid? JobId { get; init; }
    public required IReadOnlyList<AgentLink> Links { get; init; }
    public required IReadOnlyList<string> Replies { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
