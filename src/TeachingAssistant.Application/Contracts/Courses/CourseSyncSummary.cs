namespace TeachingAssistant.Application.Contracts;

public record CourseSyncSummary
{
    public required int Students { get; init; }
    public required int NewStudents { get; init; }
    public required int LeftStudents { get; init; }
    public required int Assignments { get; init; }
    public required int Submissions { get; init; }
    public required int NewSubmissions { get; init; }
    public required int Graded { get; init; }
    public required int PhotosUpdated { get; init; }
    public required int PhotosFailed { get; init; }
}
