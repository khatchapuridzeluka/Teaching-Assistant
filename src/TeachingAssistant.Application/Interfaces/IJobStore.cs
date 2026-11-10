using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Interfaces;

public interface IJobStore
{
    Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default);

    Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default);

    Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetRecentAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(
        DateTimeOffset finishedSince, JobType? type, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobChildCount>> CountChildrenAsync(IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetOpenGroupsAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IJobSignal
{
    void Notify();
}
