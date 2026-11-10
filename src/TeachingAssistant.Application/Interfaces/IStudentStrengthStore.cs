using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Interfaces;

public interface IStudentStrengthStore
{
    Task<IReadOnlyList<StudentStrength>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentStrength>> GetByStudentAsync(
        string courseId, string studentId, CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<StudentStrength> strengths);

    void RemoveRange(IEnumerable<StudentStrength> strengths);
}
