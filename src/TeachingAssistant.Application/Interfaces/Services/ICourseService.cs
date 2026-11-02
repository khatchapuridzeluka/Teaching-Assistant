using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Courses the teacher teaches, for the Home screen.
/// </summary>
public interface ICourseService
{
    Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default);
}
