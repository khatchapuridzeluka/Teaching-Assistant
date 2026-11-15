using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface ICourseGuidanceStore
{
    Task<CourseGuidance?> FindAsync(string courseId, CancellationToken cancellationToken = default);

    void Add(CourseGuidance guidance);

    void Remove(CourseGuidance guidance);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
