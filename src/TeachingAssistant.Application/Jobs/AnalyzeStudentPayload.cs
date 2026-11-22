using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Jobs;

public record AnalyzeStudentPayload
{
    public required string CourseId { get; init; }
    public required string StudentId { get; init; }
    public string? StudentName { get; init; }
    public StudentAnalysisKind Kind { get; init; } = StudentAnalysisKind.Mistakes;
}
