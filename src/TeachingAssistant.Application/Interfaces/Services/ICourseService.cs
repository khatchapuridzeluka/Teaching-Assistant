using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface ICourseService
{
    Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default);
}
