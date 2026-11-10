using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Interfaces;

public interface IStudentAnalysisStore
{
    Task<StudentAnalysis?> GetLatestAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default);

    void Add(StudentAnalysis analysis);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
