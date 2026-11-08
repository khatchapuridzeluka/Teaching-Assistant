namespace TeachingAssistant.Domain.Jobs;

/// <summary>
/// One piece of background work (grade a student, fetch an assignment, sync a course), stored so it survives restarts and
/// the teacher can follow it. The row is the queue entry, its status and its history at once.
/// <para>
/// Life cycle: created <see cref="JobStatus.Queued"/>; a worker <see cref="Start"/>s it, holding it for a lease; it ends
/// <see cref="Succeed"/>ed, <see cref="Fail"/>ed or canceled. A temporary failure goes back to Queued with a growing wait
/// (<see cref="RetryDelay"/>) until <see cref="MaxAttempts"/> is reached. A worker that died mid-job is noticed when its
/// lease runs out (<see cref="RecoverExpiredLease"/>) and counts as a failed attempt. Because a job can run more than once,
/// handlers must be safe to repeat.
/// </para>
/// </summary>
public class Job
{
    /// <summary>Wait after the first failed attempt; doubled after each further one.</summary>
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>The longest wait between attempts.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(10);

    // For EF Core.
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

    /// <summary>The job's inputs as JSON, shaped by its type (e.g. {"versionId": "…"}).</summary>
    public string Payload { get; private set; } = null!;

    public JobStatus Status { get; private set; }

    /// <summary>The parent job when this is one of a group (one student of "grade all").</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>"The same work": while one job with this key is queued or running, no other may be (e.g. fetch:{assignment}).</summary>
    public string? DedupKey { get; private set; }

    /// <summary>For listing a course's or an assignment's jobs without reading the payload.</summary>
    public string? CourseId { get; private set; }

    public string? AssignmentId { get; private set; }

    public int? ProgressDone { get; private set; }
    public int? ProgressTotal { get; private set; }

    /// <summary>What the job produced, as JSON (e.g. {"gradingId": "…"}); null until it succeeded.</summary>
    public string? Result { get; private set; }

    /// <summary>Why the last attempt failed; kept for the teacher.</summary>
    public string? Error { get; private set; }

    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }

    /// <summary>Not to be started before this time (now for new jobs, later for retries).</summary>
    public DateTimeOffset RunAfter { get; private set; }

    /// <summary>The worker running it, and until when it is trusted to still be alive.</summary>
    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>The teacher asked to stop it while it was running; the worker stops at its next check.</summary>
    public bool CancelRequested { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the first attempt started.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public bool IsFinished => Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Canceled;

    /// <summary>Queued and its time has come.</summary>
    public bool CanStart(DateTimeOffset now) => Status == JobStatus.Queued && RunAfter <= now;

    /// <summary>Running, but its worker's lease ran out: the worker most likely died.</summary>
    public bool HasExpiredLease(DateTimeOffset now) => Status == JobStatus.Running && LockedUntil < now;

    /// <summary>The wait before the next attempt after the given number of failed ones: 30 s, 1 min, 2 min … up to 10 min.</summary>
    public static TimeSpan RetryDelay(int failedAttempts)
    {
        var doublings = Math.Clamp(failedAttempts - 1, 0, 30);
        var delay = FirstRetryDelay * Math.Pow(2, doublings);
        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }

    /// <summary>A worker takes the job and holds it until now + lease; counts one attempt.</summary>
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

    /// <summary>The worker is still alive and working: hold the job longer (for long jobs such as grading all).</summary>
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

    /// <summary>
    /// An attempt failed. A temporary failure (model overloaded, Google unavailable) is queued again after
    /// <see cref="RetryDelay"/> while attempts are left; a permanent one (nothing to grade, bad input) or the last attempt
    /// fails the job.
    /// </summary>
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

    /// <summary>The worker died (its lease ran out): counts as a temporary failure of that attempt.</summary>
    public void RecoverExpiredLease(DateTimeOffset now)
    {
        if (!HasExpiredLease(now))
        {
            throw new InvalidOperationException($"Job {Id} has no expired lease.");
        }

        Fail($"The worker stopped while running this job (lease expired after attempt {Attempts}).", temporary: true, now);
    }

    /// <summary>
    /// The teacher stops the job. Queued: canceled at once. Running: marked, and the worker stops at its next check and
    /// calls <see cref="ConfirmCanceled"/>. Finished: nothing to do.
    /// </summary>
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

    /// <summary>The worker stopped a running job the teacher canceled.</summary>
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
