using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeTestRuns : ITestRunStore
{
    public List<TestRun> All { get; } = [];

    public Task<IReadOnlyList<TestRun>> GetLatestByTestSetAsync(Guid testSetId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TestRun>>(All.Where(r => r.TestSetId == testSetId).AsEnumerable().Reverse().DistinctBy(r => r.VersionId).ToList());

    public Task<TestRun?> FindLatestWithAdapterAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.LastOrDefault(r => r.VersionId == versionId && r.Status == TestRunStatus.Completed && r.Adapter != null));

    public void Add(TestRun run) => All.Add(run);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
