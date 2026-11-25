using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface ITestRunService
{
    Task RunAsync(Guid testSetId, Guid versionId, CancellationToken cancellationToken = default);

    Task<RunTestsResponse?> QueueAllAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TestRunResponse>?> GetLatestAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default);
}
