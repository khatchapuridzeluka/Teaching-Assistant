using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

public interface ICourseAssignmentStore
{
    Task<IReadOnlyList<CourseAssignment>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default);

    void Add(CourseAssignment assignment);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
