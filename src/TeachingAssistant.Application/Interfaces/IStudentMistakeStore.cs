using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Interfaces;

public interface IStudentMistakeStore
{
    Task<IReadOnlyList<StudentMistake>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentMistake>> GetByStudentAsync(
        string courseId, string studentId, CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<StudentMistake> mistakes);

    void RemoveRange(IEnumerable<StudentMistake> mistakes);
}
