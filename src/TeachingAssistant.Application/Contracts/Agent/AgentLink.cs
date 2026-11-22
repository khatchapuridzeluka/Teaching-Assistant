namespace TeachingAssistant.Application.Contracts;

public record AgentLink(
    AgentLinkKind Kind,
    string Label,
    string CourseId,
    string? AssignmentId = null,
    string? SubmissionId = null,
    string? StudentId = null,
    Guid? GradingId = null)
{
    public string Key => $"{Kind}:{CourseId}:{AssignmentId}:{SubmissionId}:{StudentId}:{GradingId}";
}
