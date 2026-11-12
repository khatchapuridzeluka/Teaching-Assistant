using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Contracts;

public record StartAgentRunRequest(
    string Text, Guid? ConversationId = null, string? Page = null, string? CourseId = null, string? AssignmentId = null);

public record AgentRunResponse(
    Guid Id,
    Guid ConversationId,
    AgentRunStatus Status,
    string? Reply,
    string? Error,
    int Steps,
    Guid? WaitingJobId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<AgentMessageResponse> Messages);

public record AgentMessageResponse(
    Guid Id,
    Guid RunId,
    int Sequence,
    AgentMessageRole Role,
    string Text,
    IReadOnlyList<string> ToolCalls,
    string? ToolName,
    Guid? JobId,
    DateTimeOffset CreatedAt);

public record AgentConversationResponse(Guid Id, Guid? ActiveRunId, IReadOnlyList<AgentMessageResponse> Messages);
