using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface ITestDesignService
{
    Task<Guid> GenerateAsync(string courseId, string assignmentId, GenerateTestsRequest request, CancellationToken cancellationToken = default);

    Task<TestSetResponse?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default);

    Task<TestSetResponse?> SaveAsync(
        string assignmentId, Guid testSetId, SaveTestSetRequest request, CancellationToken cancellationToken = default);

    Task<TestSetResponse?> ApproveAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default);
}
