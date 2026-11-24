using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Interfaces;

public interface ITestSetStore
{
    Task<TestSet?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default);

    Task<TestSet?> GetLatestApprovedAsync(string assignmentId, CancellationToken cancellationToken = default);

    Task<TestSet?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(TestSet testSet);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
