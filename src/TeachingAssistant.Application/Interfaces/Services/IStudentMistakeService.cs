using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IStudentMistakeService
{
    Task<StudentMistakesResponse> GetAsync(
        string courseId, string studentId, StudentMistakeFilter? filter = null, CancellationToken cancellationToken = default);
}
