namespace TeachingAssistant.Domain.Jobs;

public class Job
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
    ];

    private Job()
    {
    }

    public Job(
        JobType type,
        string payload,
        DateTimeOffset createdAt,
        Guid? parentId = null,
        string? dedupKey = null,
        string? courseId = null,
        string? assignmentId = null,
        int maxAttempts = 3,
        DateTimeOffset? runAfter = null)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "A job needs at least one attempt.");
        }

        Id = Guid.CreateVersion7();
        Type = type;
        Payload = payload;
        Status = JobStatus.Queued;
        ParentId = parentId;
        DedupKey = dedupKey;
        CourseId = courseId;
        AssignmentId = assignmentId;
        MaxAttempts = maxAttempts;
        CreatedAt = createdAt.ToUniversalTime();
        RunAfter = (runAfter ?? createdAt).ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public JobType Type { get; private set; }

    public string Payload { get; private set; } = null!;

    public JobStatus Status { get; private set; }

    public Guid? ParentId { get; private set; }

    public string? DedupKey { get; private set; }

    public string? CourseId { get; private set; }

    public string? AssignmentId { get; private set; }

    public int? ProgressDone { get; private set; }
    public int? ProgressTotal { get; private set; }

    public string? Result { get; private set; }

    public string? Error { get; private set; }

    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }

    public DateTimeOffset RunAfter { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public bool CancelRequested { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public bool IsFinished => Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Canceled;

    public bool IsGroup => IsGroupType(Type);

    /// <summary>Whether jobs of this type only group child jobs and finish when their children do.</summary>
    public static bool IsGroupType(JobType type) => type is JobType.GradeAssignment or JobType.GradeStudent or JobType.EvaluationRun;

    public static Job Group(
        JobType type, string payload, DateTimeOffset createdAt, string? dedupKey = null, string? courseId = null, string? assignmentId = null)
    {
        if (!IsGroupType(type))
        {
            throw new ArgumentException($"{type} is not a job that groups other jobs.", nameof(type));
        }

        var job = new Job(type, payload, createdAt, dedupKey: dedupKey, courseId: courseId, assignmentId: assignmentId, maxAttempts: 1)
        {
            Status = JobStatus.Running,
            Attempts = 1,
            ProgressDone = 0,
            ProgressTotal = 0,
        };
        job.StartedAt = job.CreatedAt;
        return job;
    }

    public void TrackChildren(int total, int finished, int failed, string? result, DateTimeOffset now)
    {
        if (!IsGroup)
        {
            throw new InvalidOperationException($"Job {Id} is not a group of jobs.");
        }

        if (total < 0 || finished < 0 || finished > total || failed < 0 || failed > finished)
        {
            throw new ArgumentOutOfRangeException(nameof(finished), $"{finished} finished and {failed} failed of {total} is not possible.");
        }

        if (Status != JobStatus.Running)
        {
            return;
        }

        ProgressTotal = total;
        ProgressDone = finished;
        if (finished < total)
        {
            return;
        }

        Result = result;
        if (CancelRequested)
        {
            Status = JobStatus.Canceled;
        }
        else if (failed > 0)
        {
            Status = JobStatus.Failed;
            Error = $"{failed} of {total} failed.";
        }
        else
        {
            Status = JobStatus.Succeeded;
        }

        Finish(now);
    }

    public bool CanStart(DateTimeOffset now) => Status == JobStatus.Queued && RunAfter <= now;

    public bool HasExpiredLease(DateTimeOffset now) => Status == JobStatus.Running && LockedUntil < now;

    public static TimeSpan RetryDelay(int failedAttempts)
    {
        if (failedAttempts < 1)
        {
            return RetryDelays[0];
        }

        if (failedAttempts > RetryDelays.Length)
        {
            return RetryDelays[^1];
        }

        return RetryDelays[failedAttempts - 1];
    }

    public void Start(string workerId, DateTimeOffset now, TimeSpan lease)
    {
        if (!CanStart(now))
        {
            throw new InvalidOperationException($"Job {Id} cannot start: it is {Status}, to run after {RunAfter:O}.");
        }

        Status = JobStatus.Running;
        Attempts++;
        LockedBy = workerId;
        LockedUntil = (now + lease).ToUniversalTime();
        StartedAt ??= now.ToUniversalTime();
    }

    public void SetChildTotal(int total)
    {
        if (!IsGroup || Status != JobStatus.Running)
        {
            throw new InvalidOperationException($"Job {Id} is not a running group of jobs.");
        }

        ReportProgress(Math.Min(ProgressDone ?? 0, total), total);
    }

    public void ExtendLease(DateTimeOffset now, TimeSpan lease)
    {
        EnsureRunning();
        LockedUntil = (now + lease).ToUniversalTime();
    }

    public void ReportProgress(int done, int total)
    {
        if (total < 0 || done < 0 || done > total)
        {
            throw new ArgumentOutOfRangeException(nameof(done), $"Progress {done}/{total} is not possible.");
        }

        ProgressDone = done;
        ProgressTotal = total;
    }

    public void Succeed(string? result, DateTimeOffset now)
    {
        EnsureRunning();
        Status = JobStatus.Succeeded;
        Result = result;
        Error = null;
        Finish(now);
    }

    public void Fail(string error, bool temporary, DateTimeOffset now)
    {
        EnsureRunning();
        Error = error;
        LockedBy = null;
        LockedUntil = null;

        if (temporary && Attempts < MaxAttempts && !CancelRequested)
        {
            Status = JobStatus.Queued;
            RunAfter = (now + RetryDelay(Attempts)).ToUniversalTime();
            return;
        }

        Status = CancelRequested ? JobStatus.Canceled : JobStatus.Failed;
        Finish(now);
    }

    public void RecoverExpiredLease(DateTimeOffset now)
    {
        if (!HasExpiredLease(now))
        {
            throw new InvalidOperationException($"Job {Id} has no expired lease.");
        }

        Fail($"The worker stopped while running this job (lease expired after attempt {Attempts}).", temporary: true, now);
    }

    public void Cancel(DateTimeOffset now)
    {
        switch (Status)
        {
            case JobStatus.Queued:
                Status = JobStatus.Canceled;
                Finish(now);
                break;
            case JobStatus.Running:
                CancelRequested = true;
                break;
        }
    }

    public void ConfirmCanceled(DateTimeOffset now)
    {
        EnsureRunning();
        Status = JobStatus.Canceled;
        Finish(now);
    }

    private void Finish(DateTimeOffset now)
    {
        LockedBy = null;
        LockedUntil = null;
        FinishedAt = now.ToUniversalTime();
    }

    private void EnsureRunning()
    {
        if (Status != JobStatus.Running)
        {
            throw new InvalidOperationException($"Job {Id} is {Status}, not running.");
        }
    }
}
