using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface ITestRunService
{
    Task RunAsync(Guid testSetId, Guid versionId, bool newAdapter = false, CancellationToken cancellationToken = default);

    Task<RunTestsResponse?> QueueAllAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default);

    Task<JobResponse?> QueueOneAsync(
        string assignmentId, Guid testSetId, Guid versionId, bool newAdapter = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TestRunResponse>?> GetLatestAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default);
}
