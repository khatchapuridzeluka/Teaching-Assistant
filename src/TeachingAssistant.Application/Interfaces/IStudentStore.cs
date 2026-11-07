using TeachingAssistant.Domain.Profiling;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>Our copies of the students the source knows, with their enrollments loaded.</summary>
public interface IStudentStore
{
    /// <summary>The students with any of these source user IDs, with all their enrollments.</summary>
    Task<IReadOnlyList<Student>> FindByExternalIdsAsync(
        SubmissionSource source, IReadOnlyCollection<string> externalUserIds, CancellationToken cancellationToken = default);

    /// <summary>The students with an active enrollment in a course, with all their enrollments.</summary>
    Task<IReadOnlyList<Student>> GetEnrolledAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default);

    /// <summary>Every student ever enrolled in a course (also those who left), with all their enrollments.</summary>
    Task<IReadOnlyList<Student>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default);

    void Add(Student student);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
