namespace TeachingAssistant.Application.Jobs;

public record GradeStudentPayload
{
    public required string CourseId { get; init; }
    public required string StudentId { get; init; }
    public string? StudentName { get; init; }
    public bool Regrade { get; init; }
}
