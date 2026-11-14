using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IStudentOverviewService
{
    Task<StudentOverviewResponse> GetAsync(string courseId, string studentId, CancellationToken cancellationToken = default);
}
