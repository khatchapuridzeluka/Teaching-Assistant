namespace TeachingAssistant.Application.Contracts;

public record AgentLink
{
    public required AgentLinkKind Kind { get; init; }
    public required string Label { get; init; }
    public required string CourseId { get; init; }
    public string? AssignmentId { get; init; }
    public string? SubmissionId { get; init; }
    public string? StudentId { get; init; }
    public Guid? GradingId { get; init; }

    public string Key => $"{Kind}:{CourseId}:{AssignmentId}:{SubmissionId}:{StudentId}:{GradingId}";
}
