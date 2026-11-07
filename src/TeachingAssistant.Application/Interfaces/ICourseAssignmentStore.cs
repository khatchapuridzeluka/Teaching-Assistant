using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>Our copies of a course's assignments (maximum points and deadlines, for scores).</summary>
public interface ICourseAssignmentStore
{
    Task<IReadOnlyList<CourseAssignment>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default);

    void Add(CourseAssignment assignment);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
