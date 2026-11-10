using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IStudentStrengthService
{
    Task<StudentStrengthsResponse> GetAsync(
        string courseId, string studentId, StudentStrengthFilter? filter = null, CancellationToken cancellationToken = default);
}
