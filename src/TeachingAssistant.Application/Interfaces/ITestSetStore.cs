using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Interfaces;

public interface ITestSetStore
{
    Task<TestSet?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default);

    void Add(TestSet testSet);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
