using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IStudentAnalysisService
{
    Task<Guid> AnalyzeAsync(string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default);

    Task<StudentAnalysisResponse?> GetLatestAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default);
}
