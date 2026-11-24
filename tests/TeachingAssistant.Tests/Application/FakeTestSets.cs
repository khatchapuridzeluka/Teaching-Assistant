using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeTestSets : ITestSetStore
{
    public List<TestSet> All { get; } = [];

    public int Saves { get; private set; }

    public Task<TestSet?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.Where(t => t.AssignmentId == assignmentId).OrderByDescending(t => t.Version).FirstOrDefault());

    public Task<TestSet?> GetLatestApprovedAsync(string assignmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.Where(t => t.AssignmentId == assignmentId && t.Status == TestSetStatus.Approved)
            .OrderByDescending(t => t.Version).FirstOrDefault());

    public Task<TestSet?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(t => t.Id == id));

    public void Add(TestSet testSet) => All.Add(testSet);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
