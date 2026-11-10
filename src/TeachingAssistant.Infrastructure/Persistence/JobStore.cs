using Microsoft.EntityFrameworkCore;
using Npgsql;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
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

    public async Task<IReadOnlyList<Job>> GetRecentAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default)
    {
        var jobs = db.Jobs.Where(j => j.ParentId == null);
        if (status is { } wanted)
        {
            jobs = jobs.Where(j => j.Status == wanted);
        }

        if (type is { } kind)
        {
            jobs = jobs.Where(j => j.Type == kind);
        }

        if (courseId is not null)
        {
            jobs = jobs.Where(j => j.CourseId == courseId);
        }

        if (assignmentId is not null)
        {
            jobs = jobs.Where(j => j.AssignmentId == assignmentId);
        }

        return await jobs.OrderByDescending(j => j.CreatedAt).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(
        DateTimeOffset finishedSince, JobType? type, CancellationToken cancellationToken = default)
    {
        var jobs = db.Jobs.Where(j => j.ParentId == null);
        if (type is { } kind)
        {
            jobs = jobs.Where(j => j.Type == kind);
        }

        var counts = await jobs
            .Where(j => j.Status == JobStatus.Queued || j.Status == JobStatus.Running || j.FinishedAt >= finishedSince)
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    public async Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(
        IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default)
    {
        if (versionIds.Count == 0)
        {
            return [];
        }

        var rows =
            from version in db.SubmissionVersions
            join submission in db.Submissions on version.SubmissionId equals submission.Id
            where versionIds.Contains(version.Id)
            join assignment in db.CourseAssignments
                on new { submission.Source, submission.CourseId, Id = submission.AssignmentId }
                equals new { assignment.Source, assignment.CourseId, Id = assignment.ExternalAssignmentId } into assignments
            from assignment in assignments.DefaultIfEmpty()
            select new JobSubject(version.Id, version.Number, submission.CourseId, submission.AssignmentId,
                assignment == null ? null : assignment.Title, submission.ExternalSubmissionId, submission.StudentId,
                submission.StudentName);

        return await rows.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Where(j => j.ParentId == parentId)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JobChildCount>> CountChildrenAsync(
        IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken = default)
    {
        if (parentIds.Count == 0)
        {
            return [];
        }

        return await db.Jobs
            .Where(j => j.ParentId != null && parentIds.Contains(j.ParentId.Value))
            .GroupBy(j => new { ParentId = j.ParentId!.Value, j.Status })
            .Select(g => new JobChildCount(g.Key.ParentId, g.Key.Status, g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetOpenGroupsAsync(CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Where(j => j.Type == JobType.GradeAssignment && j.Status == JobStatus.Running)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    private Task<Job?> FindActiveAsync(string dedupKey, CancellationToken cancellationToken) =>
        db.Jobs.FirstOrDefaultAsync(
            j => j.DedupKey == dedupKey && (j.Status == JobStatus.Queued || j.Status == JobStatus.Running), cancellationToken);
}
