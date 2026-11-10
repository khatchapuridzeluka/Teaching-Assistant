using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Domain;

public class JobTests
{
    [Fact]
    public void AGroup_StartsRunningWithoutAWorker_SoItIsNeverClaimedOrRecovered()
    {
        var now = DateTimeOffset.UtcNow;
        var group = Job.Group(JobType.GradeAssignment, "{}", now);

        Assert.Equal((JobStatus.Running, (string?)null), (group.Status, group.LockedBy));
        Assert.False(group.CanStart(now.AddHours(1)));
        Assert.False(group.HasExpiredLease(now.AddDays(1)));
        Assert.Throws<ArgumentException>(() => Job.Group(JobType.GradeVersion, "{}", now));
        Assert.True(Job.Group(JobType.GradeStudent, "{}", now).IsGroup);
    }

    [Fact]
    public void AGroup_FinishesOnlyWhenEveryChildHasFinished()
    {
        var now = DateTimeOffset.UtcNow;
        var group = Job.Group(JobType.GradeAssignment, "{}", now);
        group.SetChildTotal(3);

        group.TrackChildren(3, 2, 0, null, now);
        Assert.Equal((JobStatus.Running, 2), (group.Status, group.ProgressDone));

        group.TrackChildren(3, 3, 0, "{}", now);
        Assert.Equal((JobStatus.Succeeded, "{}"), (group.Status, group.Result));
    }

    [Fact]
    public void AGroup_FailsWhenAChildFailed_AndIsCanceledWhenItWasCanceled()
    {
        var now = DateTimeOffset.UtcNow;
        var failed = Job.Group(JobType.GradeAssignment, "{}", now);
        failed.TrackChildren(4, 4, 1, null, now);

        var canceled = Job.Group(JobType.GradeAssignment, "{}", now);
        canceled.Cancel(now);
        canceled.TrackChildren(2, 2, 0, null, now);

        Assert.Equal((JobStatus.Failed, "1 of 4 failed."), (failed.Status, failed.Error));
        Assert.Equal(JobStatus.Canceled, canceled.Status);
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    [Fact]
    public void NewJob_IsQueued_AndCanStartRightAway()
    {
        var job = NewJob();

        Assert.Equal((JobStatus.Queued, 0), (job.Status, job.Attempts));
        Assert.True(job.CanStart(Now));
        Assert.False(job.IsFinished);
    }

    [Fact]
    public void Start_TakesTheJob_ForALease()
    {
        var job = NewJob();

        job.Start("worker-1", Now, Lease);

        Assert.Equal((JobStatus.Running, 1, "worker-1"), (job.Status, job.Attempts, job.LockedBy));
        Assert.Equal(Now + Lease, job.LockedUntil);
        Assert.Equal(Now, job.StartedAt);
        Assert.Throws<InvalidOperationException>(() => job.Start("worker-2", Now, Lease)); // already taken
    }

    [Fact]
    public void Succeed_StoresTheResult_AndReleasesTheJob()
    {
        var job = NewJob();
        job.Start("worker-1", Now, Lease);

        job.Succeed("""{"gradingId":"g1"}""", Now.AddSeconds(40));

        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Equal("""{"gradingId":"g1"}""", job.Result);
        Assert.Equal((null, null), (job.LockedBy, job.LockedUntil));
        Assert.Equal(Now.AddSeconds(40), job.FinishedAt);
        Assert.True(job.IsFinished);
    }

    [Fact]
    public void TemporaryFailure_IsRetriedLater_WithGrowingWaits_UntilAttemptsRunOut()
    {
        var job = NewJob(maxAttempts: 3);

        job.Start("w", Now, Lease);
        job.Fail("Model overloaded", temporary: true, Now);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(Now.AddSeconds(30), job.RunAfter);
        Assert.False(job.CanStart(Now.AddSeconds(10))); // still waiting

        job.Start("w", Now.AddSeconds(30), Lease);
        job.Fail("Model overloaded", temporary: true, Now.AddSeconds(30));
        Assert.Equal(Now.AddSeconds(30 + 60), job.RunAfter);

        job.Start("w", Now.AddSeconds(90), Lease);
        job.Fail("Model overloaded", temporary: true, Now.AddSeconds(90));
        Assert.Equal((JobStatus.Failed, 3, "Model overloaded"), (job.Status, job.Attempts, job.Error));
        Assert.True(job.IsFinished);
    }

    [Fact]
    public void PermanentFailure_FailsAtOnce()
    {
        var job = NewJob(maxAttempts: 3);
        job.Start("w", Now, Lease);

        job.Fail("Nothing to grade", temporary: false, Now);

        Assert.Equal((JobStatus.Failed, 1), (job.Status, job.Attempts));
    }

    [Fact]
    public void RetryDelay_FollowsTheList_AndStaysAtTheLastWait()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), Job.RetryDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(1), Job.RetryDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(2), Job.RetryDelay(3));
        Assert.Equal(TimeSpan.FromMinutes(5), Job.RetryDelay(4));
        Assert.Equal(TimeSpan.FromMinutes(10), Job.RetryDelay(5));
        Assert.Equal(TimeSpan.FromMinutes(10), Job.RetryDelay(8));
        Assert.Equal(TimeSpan.FromSeconds(30), Job.RetryDelay(0));
        Assert.Equal(TimeSpan.FromMinutes(10), Job.RetryDelay(50));
    }

    [Fact]
    public void ExpiredLease_CountsAsAFailedAttempt_AndIsQueuedAgain()
    {
        var job = NewJob();
        job.Start("worker-1", Now, Lease);

        Assert.False(job.HasExpiredLease(Now.AddMinutes(5)));
        Assert.True(job.HasExpiredLease(Now.AddMinutes(11)));

        job.RecoverExpiredLease(Now.AddMinutes(11));

        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Null(job.LockedBy);
        Assert.Contains("lease expired", job.Error);
    }

    [Fact]
    public void ExtendLease_KeepsALongJobAlive()
    {
        var job = NewJob();
        job.Start("w", Now, Lease);

        job.ExtendLease(Now.AddMinutes(9), Lease);

        Assert.False(job.HasExpiredLease(Now.AddMinutes(15)));
    }

    [Fact]
    public void Cancel_QueuedJob_StopsAtOnce_RunningJob_StopsAtTheWorkersNextCheck()
    {
        var queued = NewJob();
        queued.Cancel(Now);
        Assert.Equal(JobStatus.Canceled, queued.Status);

        var running = NewJob();
        running.Start("w", Now, Lease);
        running.Cancel(Now);
        Assert.Equal((JobStatus.Running, true), (running.Status, running.CancelRequested));

        running.ConfirmCanceled(Now.AddSeconds(5));
        Assert.Equal(JobStatus.Canceled, running.Status);
        Assert.True(running.IsFinished);
    }

    [Fact]
    public void CanceledWhileRunning_IsNotRetried_WhenItThenFails()
    {
        var job = NewJob();
        job.Start("w", Now, Lease);
        job.Cancel(Now);

        job.Fail("Model overloaded", temporary: true, Now);

        Assert.Equal(JobStatus.Canceled, job.Status);
    }

    [Fact]
    public void Progress_MustBePossible()
    {
        var job = NewJob();

        job.ReportProgress(9, 15);

        Assert.Equal((9, 15), (job.ProgressDone!.Value, job.ProgressTotal!.Value));
        Assert.Throws<ArgumentOutOfRangeException>(() => job.ReportProgress(16, 15));
    }

    [Fact]
    public void ChildJob_KnowsItsParent()
    {
        var parent = new Job(JobType.GradeAssignment, """{"assignmentId":"a1"}""", Now, assignmentId: "a1");
        var child = new Job(JobType.GradeVersion, """{"versionId":"v1"}""", Now, parentId: parent.Id, assignmentId: "a1");

        Assert.Equal(parent.Id, child.ParentId);
    }

    private static Job NewJob(int maxAttempts = 3) =>
        new(JobType.GradeVersion, """{"versionId":"v1"}""", Now, dedupKey: "grade:v1", maxAttempts: maxAttempts);
}
