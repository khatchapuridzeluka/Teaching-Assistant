using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Contracts;

public record AgentRunResponse(
    Guid Id,
    Guid ConversationId,
    AgentRunStatus Status,
    string? Reply,
    string? Error,
    int Steps,
    IReadOnlyList<Guid> WaitingJobIds,
    DateTimeOffset? WaitUntil,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<AgentMessageResponse> Messages);
