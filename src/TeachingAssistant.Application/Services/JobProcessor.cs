using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class JobProcessor : IJobProcessor
{
    private readonly IJobStore _jobs;
    private readonly TimeProvider _timeProvider;

    public JobProcessor(IJobStore jobs, TimeProvider timeProvider)
    {
        _jobs = jobs;
        _timeProvider = timeProvider;
    }

    public async Task<ClaimedJob?> ClaimNextAsync(string workerId, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.ClaimNextAsync(workerId, _timeProvider.GetUtcNow(), lease, cancellationToken);
        return job is null ? null : new ClaimedJob(job.Id, job.Type, job.Payload);
    }

    public async Task<bool> HeartbeatAsync(Guid jobId, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.FindAsync(jobId, cancellationToken);
        if (job is not { Status: JobStatus.Running })
        {
            return false;
        }

        job.ExtendLease(_timeProvider.GetUtcNow(), lease);
        await _jobs.SaveChangesAsync(cancellationToken);
        return !job.CancelRequested;
    }

    public async Task CompleteAsync(Guid jobId, string? result, CancellationToken cancellationToken = default)
    {
        var job = await RunningAsync(jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Succeed(result, _timeProvider.GetUtcNow());
        await _jobs.SaveChangesAsync(cancellationToken);
        await UpdateParentAsync(job, cancellationToken);
    }

    public async Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default)
    {
        var job = await RunningAsync(jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Fail(exception.Message, JobErrors.IsTemporary(exception), _timeProvider.GetUtcNow());
        await _jobs.SaveChangesAsync(cancellationToken);
        await UpdateParentAsync(job, cancellationToken);
    }

    public async Task ConfirmCanceledAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await RunningAsync(jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.ConfirmCanceled(_timeProvider.GetUtcNow());
        await _jobs.SaveChangesAsync(cancellationToken);
        await UpdateParentAsync(job, cancellationToken);
    }

    public async Task<int> ReconcileGroupsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var groups = await _jobs.GetOpenGroupsAsync(cancellationToken);
        var finished = 0;
        foreach (var group in groups)
        {
            JobGroups.Track(group, await _jobs.GetChildrenAsync(group.Id, cancellationToken), now);
            if (group.IsFinished)
            {
                finished++;
            }
        }

        if (groups.Count > 0)
        {
            await _jobs.SaveChangesAsync(cancellationToken);
        }

        return finished;
    }

    private async Task UpdateParentAsync(Job child, CancellationToken cancellationToken)
    {
        if (child.ParentId is not { } parentId || !child.IsFinished)
        {
            return;
        }

        var parent = await _jobs.FindAsync(parentId, cancellationToken);
        if (parent is not { IsGroup: true, Status: JobStatus.Running })
        {
            return;
        }

        JobGroups.Track(parent, await _jobs.GetChildrenAsync(parentId, cancellationToken), _timeProvider.GetUtcNow());
        await _jobs.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var expired = await _jobs.GetExpiredLeasesAsync(now, cancellationToken);
        foreach (var job in expired)
        {
            job.RecoverExpiredLease(now);
        }

        if (expired.Count > 0)
        {
            await _jobs.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }

    private async Task<Job?> RunningAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobs.FindAsync(jobId, cancellationToken);
        return job is { Status: JobStatus.Running } ? job : null;
    }
}
