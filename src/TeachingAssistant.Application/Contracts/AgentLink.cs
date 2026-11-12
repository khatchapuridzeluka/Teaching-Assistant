namespace TeachingAssistant.Application.Contracts;

public enum AgentLinkKind
{
    CommonMistakes,

    Submission,

    Assignment,

    Student,
}

public record AgentLink(
    AgentLinkKind Kind, string Label, string CourseId, string? AssignmentId = null, string? SubmissionId = null, string? StudentId = null)
{
    public string Key => $"{Kind}:{CourseId}:{AssignmentId}:{SubmissionId}:{StudentId}";
}
