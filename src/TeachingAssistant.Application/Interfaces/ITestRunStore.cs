using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Interfaces;

public interface ITestRunStore
{
    /// <summary>The newest run of each submission version for this test set.</summary>
    Task<IReadOnlyList<TestRun>> GetLatestByTestSetAsync(Guid testSetId, CancellationToken cancellationToken = default);

    /// <summary>The newest run of a version whose adapter compiled, for any test set; its adapter is reused when the interface is the same.</summary>
    Task<TestRun?> FindLatestWithAdapterAsync(Guid versionId, CancellationToken cancellationToken = default);

    void Add(TestRun run);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
