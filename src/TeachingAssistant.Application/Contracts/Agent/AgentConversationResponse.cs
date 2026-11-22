namespace TeachingAssistant.Application.Contracts;

public record AgentConversationResponse
{
    public required Guid Id { get; init; }
    public Guid? ActiveRunId { get; init; }
    public required IReadOnlyList<AgentMessageResponse> Messages { get; init; }
}
