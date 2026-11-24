using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface ITestDesignService
{
    Task<Guid> GenerateAsync(string courseId, string assignmentId, GenerateTestsRequest request, CancellationToken cancellationToken = default);

    Task<TestSetResponse?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default);
}
