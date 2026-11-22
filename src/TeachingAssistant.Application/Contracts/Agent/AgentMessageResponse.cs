using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Contracts;

public record AgentMessageResponse(
    Guid Id,
    Guid RunId,
    int Sequence,
    AgentMessageRole Role,
    string Text,
    IReadOnlyList<string> ToolCalls,
    string? ToolName,
    Guid? JobId,
    IReadOnlyList<AgentLink> Links,
    IReadOnlyList<string> Replies,
    DateTimeOffset CreatedAt);
