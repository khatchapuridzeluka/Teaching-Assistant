using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Infrastructure.Persistence;

public class TestSetStore(AppDbContext db) : ITestSetStore
{
    public Task<TestSet?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default) =>
        db.TestSets
            .Where(t => t.AssignmentId == assignmentId)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<TestSet?> GetLatestApprovedAsync(string assignmentId, CancellationToken cancellationToken = default) =>
        db.TestSets
            .Where(t => t.AssignmentId == assignmentId && t.Status == TestSetStatus.Approved)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<TestSet?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.TestSets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void Add(TestSet testSet) => db.TestSets.Add(testSet);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
