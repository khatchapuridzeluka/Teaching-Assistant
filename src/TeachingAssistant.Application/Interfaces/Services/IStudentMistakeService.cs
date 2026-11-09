using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IStudentMistakeService
{
    Task<IReadOnlyList<StudentMistakeResponse>> GetAsync(string courseId, string studentId, CancellationToken cancellationToken = default);
}
