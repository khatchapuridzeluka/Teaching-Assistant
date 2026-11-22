namespace TeachingAssistant.Application.Contracts;

public record StrengthAssignmentResponse
{
    public required string AssignmentId { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public required int Strengths { get; init; }
    public required int Mistakes { get; init; }
}
