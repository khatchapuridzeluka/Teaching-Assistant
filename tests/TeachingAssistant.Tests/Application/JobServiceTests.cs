using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Application;

public class JobServiceTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    private readonly FakeJobStore _store = new();
    private readonly FakeGrading _grading = new();
    private readonly FakeSignal _signal = new();

    private JobService Service() => new(_store, _grading, _signal, TimeProvider.System);
    private JobProcessor Processor() => new(_store, TimeProvider.System);

    [Fact]
    public async Task QueueGrade_CreatesAQueuedJob_AndWakesTheWorker()
    {
        var versionId = _grading.AddGradable();

        var job = await Service().QueueGradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.NotNull(job);
        Assert.Equal((JobType.GradeVersion, JobStatus.Queued, versionId), (job.Type, job.Status, job.VersionId));
        var stored = Assert.Single(_store.Jobs);
        Assert.Equal(("assignment-1", $"grade:{versionId}"), (stored.AssignmentId, stored.DedupKey));
        Assert.Equal(1, _signal.Notified);
    }

    [Fact]
    public async Task QueueGrade_Twice_ReturnsTheSameActiveJob()
    {
        var versionId = _grading.AddGradable();

        var first = await Service().QueueGradeVersionAsync(versionId, TestContext.Current.CancellationToken);
        var second = await Service().QueueGradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Equal(first!.Id, second!.Id);
        Assert.Single(_store.Jobs);
    }

    [Fact]
    public async Task QueueGrade_UnknownVersion_IsNull_AndNotGradable_Throws()
    {
        Assert.Null(await Service().QueueGradeVersionAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        var notGradable = _grading.AddNotGradable();
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().QueueGradeVersionAsync(notGradable, TestContext.Current.CancellationToken));
        Assert.Empty(_store.Jobs);
    }

    [Fact]
    public async Task Worker_ClaimsRunsAndCompletes_TheResultPointsAtTheDraft()
    {
        var versionId = _grading.AddGradable();
        var queued = await Service().QueueGradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        var claimed = await Processor().ClaimNextAsync("worker-1", Lease, TestContext.Current.CancellationToken);
        Assert.NotNull(claimed);
        Assert.Null(await Processor().ClaimNextAsync("worker-2", Lease, TestContext.Current.CancellationToken)); // taken

        var result = await new GradeVersionJobHandler(_grading).RunAsync(claimed.Payload, TestContext.Current.CancellationToken);
        await Processor().CompleteAsync(claimed.Id, result, TestContext.Current.CancellationToken);

        var done = await Service().GetAsync(queued!.Id, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Succeeded, done!.Status);
        Assert.Equal(_grading.LastGradingId, done.GradingId);
    }

    [Fact]
    public async Task TemporaryFailure_IsRetriedLater_PermanentFailure_FailsAtOnce()
    {
        var temporary = await Service().QueueGradeVersionAsync(_grading.AddGradable(), TestContext.Current.CancellationToken);
        var claimed = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);
        await Processor().FailAsync(claimed!.Id, new LanguageModelUnavailableException("Overloaded"), TestContext.Current.CancellationToken);

        var permanent = await Service().QueueGradeVersionAsync(_grading.AddGradable(), TestContext.Current.CancellationToken);
        var claimed2 = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);
        await Processor().FailAsync(claimed2!.Id, new InvalidRequestException("Nothing to grade"), TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Queued, (await Service().GetAsync(temporary!.Id, TestContext.Current.CancellationToken))!.Status);
        var failed = await Service().GetAsync(permanent!.Id, TestContext.Current.CancellationToken);
        Assert.Equal((JobStatus.Failed, "Nothing to grade"), (failed!.Status, failed.Error));
    }

    [Fact]
    public async Task CancelingARunningJob_StopsItAtTheNextHeartbeat()
    {
        var queued = await Service().QueueGradeVersionAsync(_grading.AddGradable(), TestContext.Current.CancellationToken);
        var claimed = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);

        Assert.True(await Processor().HeartbeatAsync(claimed!.Id, Lease, TestContext.Current.CancellationToken));
        await Service().CancelAsync(queued!.Id, TestContext.Current.CancellationToken);
        Assert.False(await Processor().HeartbeatAsync(claimed.Id, Lease, TestContext.Current.CancellationToken));

        await Processor().ConfirmCanceledAsync(claimed.Id, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Canceled, (await Service().GetAsync(queued.Id, TestContext.Current.CancellationToken))!.Status);
    }

    [Fact]
    public async Task ExpiredLeases_AreRecovered_IntoTheQueue()
    {
        await Service().QueueGradeVersionAsync(_grading.AddGradable(), TestContext.Current.CancellationToken);
        var claimed = await Processor().ClaimNextAsync("w", TimeSpan.FromMilliseconds(-1), TestContext.Current.CancellationToken);

        var recovered = await Processor().RecoverExpiredLeasesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, recovered);
        Assert.Equal(JobStatus.Queued, _store.Jobs.Single(j => j.Id == claimed!.Id).Status);
    }

    [Fact]
    public async Task Overview_CountsActiveAndRecentlyFinished_AndNamesTheStudent()
    {
        var versionId = _grading.AddGradable();
        _store.Subjects.Add(new JobSubject(versionId, 2, "course-1", "assignment-1", "Assignment 2", "sub-1", "student-1", "Ana"));
        await Service().QueueGradeVersionAsync(versionId, TestContext.Current.CancellationToken);
        var other = await Service().QueueGradeVersionAsync(_grading.AddGradable(), TestContext.Current.CancellationToken);
        await Service().CancelAsync(other!.Id, TestContext.Current.CancellationToken);

        var overview = await Service().GetOverviewAsync(null, null, null, TestContext.Current.CancellationToken);

        Assert.Equal((1, 0, 1), (overview.Counts.Queued, overview.Counts.Running, overview.Counts.Canceled));
        var ana = Assert.Single(overview.Jobs, j => j.Job.VersionId == versionId);
        Assert.Equal(("Ana", "Assignment 2", "sub-1", 2), (ana.StudentName, ana.AssignmentTitle, ana.SubmissionId, ana.VersionNumber));
        Assert.Single((await Service().GetOverviewAsync(JobStatus.Canceled, null, null, TestContext.Current.CancellationToken)).Jobs);
    }

    [Fact]
    public async Task Retry_QueuesAFailedJobAgainAsANewJob_ButNotARunningOne()
    {
        var versionId = _grading.AddGradable();
        var first = await Service().QueueGradeVersionAsync(versionId, TestContext.Current.CancellationToken);
        var claimed = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().RetryAsync(first!.Id, TestContext.Current.CancellationToken));

        await Processor().FailAsync(claimed!.Id, new InvalidRequestException("Broken"), TestContext.Current.CancellationToken);
        var retried = await Service().RetryAsync(first!.Id, TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Id, retried!.Id);
        Assert.Equal((JobStatus.Queued, versionId), (retried.Status, retried.VersionId));
        Assert.Equal(JobStatus.Failed, (await Service().GetAsync(first.Id, TestContext.Current.CancellationToken))!.Status);
        Assert.Null(await Service().RetryAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    private sealed class FakeGrading : IGradingService
    {
        private readonly HashSet<Guid> _gradable = [];
        private readonly HashSet<Guid> _notGradable = [];

        public Guid LastGradingId { get; private set; }

        public Guid AddGradable()
        {
            var id = Guid.NewGuid();
            _gradable.Add(id);
            return id;
        }

        public Guid AddNotGradable()
        {
            var id = Guid.NewGuid();
            _notGradable.Add(id);
            return id;
        }

        public Task<GradableVersion?> CheckAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            if (_notGradable.Contains(versionId))
            {
                throw new InvalidRequestException("This version has nothing the model can read.");
            }

            return Task.FromResult(_gradable.Contains(versionId) ? new GradableVersion("course-1", "assignment-1") : null);
        }

        public Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            LastGradingId = Guid.NewGuid();
            return Task.FromResult<GradingResponse?>(new GradingResponse(LastGradingId, versionId, GradingStatus.Draft, "model",
                "review", 90, 100, null, [], 1, [], [], DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeSignal : IJobSignal
    {
        public int Notified { get; private set; }

        public void Notify() => Notified++;
    }

    private sealed class FakeJobStore : IJobStore
    {
        public List<Job> Jobs { get; } = [];

        public Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default)
        {
            var active = Jobs.FirstOrDefault(j => j.DedupKey == job.DedupKey && j.Status is JobStatus.Queued or JobStatus.Running);
            if (active is not null)
            {
                return Task.FromResult(active);
            }

            Jobs.Add(job);
            return Task.FromResult(job);
        }

        public Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default)
        {
            var job = Jobs.Where(j => j.CanStart(now)).OrderBy(j => j.CreatedAt).FirstOrDefault();
            job?.Start(workerId, now, lease);
            return Task.FromResult(job);
        }

        public Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Jobs.FirstOrDefault(j => j.Id == jobId));

        public Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>(Jobs.Where(j => j.AssignmentId == assignmentId).Take(limit).ToList());

        public Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>(Jobs.Where(j => j.HasExpiredLease(now)).ToList());

        public List<JobSubject> Subjects { get; } = [];

        public Task<IReadOnlyList<Job>> GetRecentAsync(
            JobStatus? status, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>(Jobs
                .Where(j => status is null || j.Status == status)
                .Where(j => courseId is null || j.CourseId == courseId)
                .Where(j => assignmentId is null || j.AssignmentId == assignmentId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(limit)
                .ToList());

        public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(
            DateTimeOffset finishedSince, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<JobStatus, int>>(Jobs
                .Where(j => !j.IsFinished || j.FinishedAt >= finishedSince)
                .GroupBy(j => j.Status)
                .ToDictionary(g => g.Key, g => g.Count()));

        public Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(
            IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobSubject>>(Subjects.Where(s => versionIds.Contains(s.VersionId)).ToList());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
