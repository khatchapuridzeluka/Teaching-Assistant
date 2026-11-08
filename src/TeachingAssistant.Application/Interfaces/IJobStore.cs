using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Interfaces;

public interface IJobStore
{
    Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default);

    Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default);

    Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IJobSignal
{
    void Notify();
}
