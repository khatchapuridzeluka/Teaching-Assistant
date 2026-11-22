namespace TeachingAssistant.Application.Jobs;

public record AgentRunPayload
{
    public required Guid RunId { get; init; }
}
