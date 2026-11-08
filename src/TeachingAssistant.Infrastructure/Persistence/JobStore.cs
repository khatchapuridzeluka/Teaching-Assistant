using Microsoft.EntityFrameworkCore;
using Npgsql;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Infrastructure.Persistence;

public class JobStore(AppDbContext db) : IJobStore
{
    public async Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job.DedupKey is not null && await FindActiveAsync(job.DedupKey, cancellationToken) is { } existing)
        {
            return existing;
        }

        db.Jobs.Add(job);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return job;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(job).State = EntityState.Detached;
            var active = await FindActiveAsync(job.DedupKey!, cancellationToken);
            if (active is null)
            {
                throw;
            }

            return active;
        }
    }

    public async Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var candidates = await db.Jobs
            .FromSql($"""
                SELECT * FROM jobs
                WHERE status = 'Queued' AND run_after <= {now}
                ORDER BY created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (candidates.FirstOrDefault() is not { } job)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        job.Start(workerId, now, lease);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        db.Jobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    public async Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Where(j => j.AssignmentId == assignmentId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Where(j => j.Status == JobStatus.Running && j.LockedUntil < now)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    private Task<Job?> FindActiveAsync(string dedupKey, CancellationToken cancellationToken) =>
        db.Jobs.FirstOrDefaultAsync(
            j => j.DedupKey == dedupKey && (j.Status == JobStatus.Queued || j.Status == JobStatus.Running), cancellationToken);
}
