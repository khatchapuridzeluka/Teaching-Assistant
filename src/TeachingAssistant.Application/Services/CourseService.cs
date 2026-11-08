using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

public class CourseService : ICourseService
{
    private readonly ISubmissionSource _submissionSource;

    public CourseService(ISubmissionSource submissionSource)
    {
        _submissionSource = submissionSource;
    }

    public async Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _submissionSource.GetCoursesAsync(cancellationToken);
        return courses.OrderBy(c => c.Name).ToList();
    }
}
