namespace TeachingAssistant.Application.Contracts;

public record ChatRequest
{
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public string? Page { get; init; }
    public string? CourseId { get; init; }
    public string? AssignmentId { get; init; }
}
