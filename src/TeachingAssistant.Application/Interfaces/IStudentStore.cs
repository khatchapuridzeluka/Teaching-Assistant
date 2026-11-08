using TeachingAssistant.Domain.Profiling;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

public interface IStudentStore
{
    Task<IReadOnlyList<Student>> FindByExternalIdsAsync(
        SubmissionSource source, IReadOnlyCollection<string> externalUserIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Student>> GetEnrolledAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Student>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default);

    void Add(Student student);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
