using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Contracts;

public record AgentRunResponse
{
    public required Guid Id { get; init; }
    public required Guid ConversationId { get; init; }
    public required AgentRunStatus Status { get; init; }
    public string? Reply { get; init; }
    public string? Error { get; init; }
    public required int Steps { get; init; }
    public required IReadOnlyList<Guid> WaitingJobIds { get; init; }
    public DateTimeOffset? WaitUntil { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public required IReadOnlyList<AgentMessageResponse> Messages { get; init; }
}
