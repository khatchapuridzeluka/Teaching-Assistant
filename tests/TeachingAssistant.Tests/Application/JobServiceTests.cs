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

        var result = await new GradeVersionJobHandler(_grading, new ModelCallContext()).RunAsync(claimed.Payload, TestContext.Current.CancellationToken);
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
        _store.Subjects.Add(new JobSubject
        {
            VersionId = versionId,
            VersionNumber = 2,
            CourseId = "course-1",
            AssignmentId = "assignment-1",
            AssignmentTitle = "Assignment 2",
            SubmissionId = "sub-1",
            StudentId = "student-1",
            StudentName = "Ana",
        });
        await Service().QueueGradeVersionAsync(versionId, TestContext.Current.CancellationToken);
        var other = await Service().QueueGradeVersionAsync(_grading.AddGradable(), TestContext.Current.CancellationToken);
        await Service().CancelAsync(other!.Id, TestContext.Current.CancellationToken);

        var overview = await Service().GetOverviewAsync(null, null, null, null, TestContext.Current.CancellationToken);

        Assert.Equal((1, 0, 1), (overview.Counts.Queued, overview.Counts.Running, overview.Counts.Canceled));
        var ana = Assert.Single(overview.Jobs, j => j.Job.VersionId == versionId);
        Assert.Equal(("Ana", "Assignment 2", "sub-1", 2), (ana.StudentName, ana.AssignmentTitle, ana.SubmissionId, ana.VersionNumber));
        Assert.Single((await Service().GetOverviewAsync(JobStatus.Canceled, null, null, null, TestContext.Current.CancellationToken)).Jobs);
        Assert.Equal(2, (await Service().GetOverviewAsync(null, JobType.GradeVersion, null, null, TestContext.Current.CancellationToken)).Jobs.Count);
        var otherWork = await Service().GetOverviewAsync(null, JobType.SyncCourse, null, null, TestContext.Current.CancellationToken);
        Assert.Equal((0, 0), (otherWork.Jobs.Count, otherWork.Counts.Queued));
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

    [Fact]
    public async Task GradeAll_QueuesAParentWithAChildPerStudent_AndReportsWhatWasSkipped()
    {
        var ana = _grading.AddGradable();
        var beka = _grading.AddGradable();
        _grading.Plan = new GradeAllPlan { VersionIds = [ana, beka], Students = 5, AlreadyDrafted = 2, NoWork = 1, Problems = [] };

        var result = await Service().QueueGradeAssignmentAsync("course-1", "assignment-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal((2, 5, 2, 1), (result.Queued, result.Students, result.AlreadyDrafted, result.NoWork));
        var parent = _store.Jobs.Single(j => j.IsGroup);
        Assert.Equal((JobStatus.Running, 2), (parent.Status, parent.ProgressTotal));
        Assert.All(_store.Jobs.Where(j => !j.IsGroup), child => Assert.Equal(parent.Id, child.ParentId));
        Assert.All(_store.Jobs.Where(j => !j.IsGroup), child => Assert.Contains("\"partOfGradeAll\":true", child.Payload));

        var context = new ModelCallContext();
        var claimed = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);
        await new GradeVersionJobHandler(_grading, context).RunAsync(claimed!.Payload, TestContext.Current.CancellationToken);
        Assert.True(context.PartOfGradeAll);
        Assert.Equal(1, _signal.Notified);

        var overview = await Service().GetOverviewAsync(null, null, null, null, TestContext.Current.CancellationToken);
        var item = Assert.Single(overview.Jobs);
        Assert.Equal((2, 1, 1), (item.Children!.Total, item.Children.Queued, item.Children.Running));
        Assert.Equal(2, (await Service().GetChildrenAsync(parent.Id, TestContext.Current.CancellationToken))!.Count);
    }

    [Fact]
    public async Task GradeAll_WithNothingToGrade_QueuesNoJob()
    {
        _grading.Plan = new GradeAllPlan { VersionIds = [], Students = 3, AlreadyDrafted = 3, NoWork = 0, Problems = [] };

        var result = await Service().QueueGradeAssignmentAsync("course-1", "assignment-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result.Job);
        Assert.Equal(3, result.AlreadyDrafted);
        Assert.Empty(_store.Jobs);
    }

    [Fact]
    public async Task TheParentFinishes_WhenItsLastChildFinishes()
    {
        _grading.Plan = new GradeAllPlan
        {
            VersionIds = [_grading.AddGradable(), _grading.AddGradable()],
            Students = 2,
            AlreadyDrafted = 0,
            NoWork = 0,
            Problems = [],
        };
        await Service().QueueGradeAssignmentAsync("course-1", "assignment-1", cancellationToken: TestContext.Current.CancellationToken);
        var parent = _store.Jobs.Single(j => j.IsGroup);

        var first = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);
        await Processor().CompleteAsync(first!.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal((JobStatus.Running, 1), (parent.Status, parent.ProgressDone));

        var second = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);
        await Processor().FailAsync(second!.Id, new InvalidRequestException("Broken"), TestContext.Current.CancellationToken);

        Assert.Equal((JobStatus.Failed, 2, "1 of 2 failed."), (parent.Status, parent.ProgressDone, parent.Error));
    }

    [Fact]
    public async Task CancelingTheParent_CancelsItsQueuedChildren_AndFinishesIt()
    {
        _grading.Plan = new GradeAllPlan
        {
            VersionIds = [_grading.AddGradable(), _grading.AddGradable()],
            Students = 2,
            AlreadyDrafted = 0,
            NoWork = 0,
            Problems = [],
        };
        var result = await Service().QueueGradeAssignmentAsync("course-1", "assignment-1", cancellationToken: TestContext.Current.CancellationToken);

        await Service().CancelAsync(result.Job!.Id, TestContext.Current.CancellationToken);

        Assert.All(_store.Jobs, j => Assert.Equal(JobStatus.Canceled, j.Status));
    }

    [Fact]
    public async Task Reconcile_FinishesAParentWhoseChildrenAllFinished()
    {
        _grading.Plan = new GradeAllPlan
        {
            VersionIds = [_grading.AddGradable()],
            Students = 1,
            AlreadyDrafted = 0,
            NoWork = 0,
            Problems = [],
        };
        await Service().QueueGradeAssignmentAsync("course-1", "assignment-1", cancellationToken: TestContext.Current.CancellationToken);
        var child = _store.Jobs.Single(j => !j.IsGroup);
        child.Start("w", DateTimeOffset.UtcNow, Lease);
        child.Succeed(null, DateTimeOffset.UtcNow);

        var finished = await Processor().ReconcileGroupsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, finished);
        Assert.Equal(JobStatus.Succeeded, _store.Jobs.Single(j => j.IsGroup).Status);
    }

    [Fact]
    public async Task GradeStudent_StartsWithAFetchStep_UnderAParentThatNamesTheStudent()
    {
        var job = await Service().QueueGradeStudentAsync("course-1", "ria", cancellationToken: TestContext.Current.CancellationToken);

        var parent = _store.Jobs.Single(j => j.IsGroup);
        var fetch = Assert.Single(_store.Jobs, j => !j.IsGroup);
        Assert.Equal((job.Id, JobType.GradeStudent, JobStatus.Running, 1), (parent.Id, parent.Type, parent.Status, parent.ProgressTotal));
        Assert.Equal((JobType.FetchStudentWork, (Guid?)parent.Id), (fetch.Type, fetch.ParentId));
        Assert.Contains(parent.Id.ToString(), fetch.Payload);
        Assert.Equal(1, _signal.Notified);

        var item = Assert.Single((await Service().GetOverviewAsync(null, null, null, null, TestContext.Current.CancellationToken)).Jobs);
        Assert.Equal(("And Ria", "ria"), (item.StudentName, item.StudentId));
    }

    [Fact]
    public async Task AfterTheFetch_GradingsAreAddedUnderTheSameParent_EachUnderItsOwnAssignment_AndTheParentWaitsForThem()
    {
        await Service().QueueGradeStudentAsync("course-1", "ria", cancellationToken: TestContext.Current.CancellationToken);
        var parent = _store.Jobs.Single(j => j.IsGroup);
        var a1 = _grading.AddGradable();
        var a5 = _grading.AddGradable();
        _grading.StudentPlan = new GradeAllPlan
        {
            VersionIds = [a1, a5],
            Students = 4,
            AlreadyDrafted = 1,
            NoWork = 1,
            Problems = ["Assignment 3: no instructions."],
            AssignmentIds = new Dictionary<Guid, string> { [a1] = "assignment-1", [a5] = "assignment-5" },
        };

        var fetch = await Processor().ClaimNextAsync("w", Lease, TestContext.Current.CancellationToken);
        var result = await Service().QueueStudentGradingsAsync(parent.Id, "course-1", "ria", 3, [], cancellationToken: TestContext.Current.CancellationToken);
        await Processor().CompleteAsync(fetch!.Id, JobPayloads.Write(result), TestContext.Current.CancellationToken);

        Assert.Equal((3, 2, 1), (result.Fetched, result.Queued, result.AlreadyDrafted));
        Assert.Equal(["Assignment 3: no instructions."], result.Problems);
        Assert.Equal(["assignment-1", "assignment-5"], _store.Jobs.Where(j => j.Type == JobType.GradeVersion).Select(j => j.AssignmentId).Order());
        Assert.All(_store.Jobs.Where(j => !j.IsGroup), j => Assert.Equal(parent.Id, j.ParentId));
        Assert.Equal((JobStatus.Running, 3, 1), (parent.Status, parent.ProgressTotal, parent.ProgressDone));
        Assert.Contains("queued 2 gradings", (await Service().GetAsync(fetch.Id, TestContext.Current.CancellationToken))!.Summary);
    }

    private sealed class FakeGrading : IGradingService
    {
        public Task<TeachingAssistant.Application.Models.EvaluationGrading?> GradeForEvaluationAsync(Guid versionId, string model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public GradingModelsResponse GetModels() =>
            throw new NotSupportedException();

        public Task<GradingResponse?> TryModelAsync(Guid versionId, string model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private readonly HashSet<Guid> _gradable = [];
        private readonly HashSet<Guid> _notGradable = [];

        public Guid LastGradingId { get; private set; }

        public GradeAllPlan Plan { get; set; } = new()
        {
            VersionIds = [],
            Students = 0,
            AlreadyDrafted = 0,
            NoWork = 0,
            Problems = [],
        };

        public GradeAllPlan StudentPlan { get; set; } = new()
        {
            VersionIds = [],
            Students = 0,
            AlreadyDrafted = 0,
            NoWork = 0,
            Problems = [],
        };

        public Task<GradeAllPlan> PlanGradeStudentAsync(string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(StudentPlan);

        public Task<string?> FindStudentNameAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("And Ria");

        public Task<GradeAllPlan> PlanGradeAllAsync(
            string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(Plan);

        public Task<IReadOnlyList<PromptPart>?> BuildInstructionsAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PromptPart>?>(null);

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

            return Task.FromResult(_gradable.Contains(versionId) ? new GradableVersion
            {
                CourseId = "course-1",
                AssignmentId = "assignment-1",
            } : null);
        }

        public Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            LastGradingId = Guid.NewGuid();
            return Task.FromResult<GradingResponse?>(new GradingResponse
            {
                Id = LastGradingId,
                VersionId = versionId,
                Status = GradingStatus.Draft,
                Model = "model",
                Review = "review",
                Score = 90,
                MaxScore = 100,
                Mistakes = [],
                InstructionsVersion = 1,
                SentFiles = [],
                SkippedFiles = [],
                CreatedAt = DateTimeOffset.UtcNow,
                Strengths = [],
            });
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

        public Task<IReadOnlyList<Job>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>(Jobs.Where(j => j.ParentId == parentId).ToList());

        public Task<IReadOnlyList<JobChildCount>> CountChildrenAsync(
            IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobChildCount>>(Jobs
                .Where(j => j.ParentId is { } parent && parentIds.Contains(parent))
                .GroupBy(j => (j.ParentId!.Value, j.Status))
                .Select(g => new JobChildCount { ParentId = g.Key.Value, Status = g.Key.Status, Count = g.Count() })
                .ToList());

        public Task<IReadOnlyList<Job>> GetOpenGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>(Jobs.Where(j => j.IsGroup && j.Status == JobStatus.Running).ToList());

        public Task<IReadOnlyList<Job>> GetRecentAsync(
            JobStatus? status, JobType? type, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>(Jobs
                .Where(j => j.ParentId == null)
                .Where(j => status is null || j.Status == status)
                .Where(j => type is null || j.Type == type)
                .Where(j => courseId is null || j.CourseId == courseId)
                .Where(j => assignmentId is null || j.AssignmentId == assignmentId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(limit)
                .ToList());

        public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(
            DateTimeOffset finishedSince, JobType? type, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<JobStatus, int>>(Jobs
                .Where(j => j.ParentId == null)
                .Where(j => type is null || j.Type == type)
                .Where(j => !j.IsFinished || j.FinishedAt >= finishedSince)
                .GroupBy(j => j.Status)
                .ToDictionary(g => g.Key, g => g.Count()));

        public Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(
            IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobSubject>>(Subjects.Where(s => versionIds.Contains(s.VersionId)).ToList());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
