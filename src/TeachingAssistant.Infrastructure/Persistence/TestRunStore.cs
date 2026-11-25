using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Infrastructure.Persistence;

public class TestRunStore(AppDbContext db) : ITestRunStore
{
    public async Task<IReadOnlyList<TestRun>> GetLatestByTestSetAsync(Guid testSetId, CancellationToken cancellationToken = default)
    {
        var runs = await db.TestRuns.Where(r => r.TestSetId == testSetId).OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        return runs.DistinctBy(r => r.VersionId).ToList();
    }

    public Task<TestRun?> FindLatestWithAdapterAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        db.TestRuns
            .Where(r => r.VersionId == versionId && r.Status == TestRunStatus.Completed && r.Adapter != null)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(TestRun run) => db.TestRuns.Add(run);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
