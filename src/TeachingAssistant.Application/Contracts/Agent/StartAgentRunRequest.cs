namespace TeachingAssistant.Application.Contracts;

public record StartAgentRunRequest
{
    public required string Text { get; init; }
    public Guid? ConversationId { get; init; }
    public string? Page { get; init; }
    public string? CourseId { get; init; }
    public string? AssignmentId { get; init; }
}
