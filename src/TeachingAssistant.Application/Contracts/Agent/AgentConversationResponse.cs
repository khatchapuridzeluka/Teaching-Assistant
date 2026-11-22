namespace TeachingAssistant.Application.Contracts;

public record AgentConversationResponse(Guid Id, Guid? ActiveRunId, IReadOnlyList<AgentMessageResponse> Messages);
