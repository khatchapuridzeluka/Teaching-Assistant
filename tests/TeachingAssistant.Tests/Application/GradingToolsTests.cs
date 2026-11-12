using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class GradingToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly Fakes _fakes = new();
    private readonly AgentRunContext _context = new();

    private GradingTools Tools()
    {
        return new GradingTools(_fakes, _fakes, _context, new Clock(Now.AddMinutes(1)));
    }

    [Fact]
    public void TheToolsAreMarked()
    {
        var names = typeof(GradingTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>()
            .Order();

        Assert.Equal(["analyze_common_mistakes", "grade_all_students"], names);
    }

    [Fact]
    public async Task GradeAll_FetchesFirst_ThenQueuesOnlyStudentsWithoutADraft()
    {
        _fakes.Result = Response(queued: 12, alreadyDrafted: 3);

        var result = await Tools().GradeAllStudentsAsync("course-1", "a-4", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([("course-1", "a-4")], _fakes.Fetches);
        Assert.Equal([false], _fakes.Regrade);
        Assert.StartsWith($"Grading queued for 12 of 15 students (job id {_fakes.Result.Job!.Id}). 3 already had an AI draft and were skipped.", result);
        Assert.Equal(_fakes.Result.Job.Id, _context.TakeQueuedJob());
    }

    [Fact]
    public async Task RegradeAll_IncludesStudentsWithADraft()
    {
        _fakes.Result = Response(queued: 15, alreadyDrafted: 3);

        var result = await Tools().GradeAllStudentsAsync("course-1", "a-4", regrade: true, TestContext.Current.CancellationToken);

        Assert.Equal([true], _fakes.Regrade);
        Assert.StartsWith($"Grading again queued for 15 of 15 students (job id {_fakes.Result.Job!.Id}). 3 of them already had a draft, which stays in the history.", result);
    }

    [Fact]
    public async Task GradeAll_PointsToRegrade_WhenEveryoneAlreadyHasADraft()
    {
        _fakes.Result = new GradeAllResponse(null, 0, 15, 15, 0, 0, 0, []);

        var result = await Tools().GradeAllStudentsAsync("course-1", "a-4", cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("Nothing to grade among the 15 students. 15 already have an AI draft; call this again with regrade set to true only if the teacher asks", result);
        Assert.Null(_context.TakeQueuedJob());
    }

    [Fact]
    public async Task GradeAll_SaysWhenOneIsAlreadyRunning()
    {
        _fakes.Result = Response(queued: 0, alreadyDrafted: 0);

        var result = await Tools().GradeAllStudentsAsync("course-1", "a-4", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal($"A Grade all for this assignment is already running (job id {_fakes.Result.Job!.Id}); nothing new was started.", result);
    }

    [Fact]
    public async Task AnalyzeCommonMistakes_QueuesTheAnalysis_AndRecordsItsJob()
    {
        var result = await Tools().AnalyzeCommonMistakesAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.Equal([("course-1", "a-4")], _fakes.Analyses);
        Assert.StartsWith($"Common mistakes analysis queued (job id {_fakes.AnalysisJob!.Id}).", result);
        Assert.Equal(_fakes.AnalysisJob.Id, _context.TakeQueuedJob());
    }

    [Fact]
    public async Task AnalyzeCommonMistakes_SaysWhenOneIsAlreadyInProgress()
    {
        _fakes.AnalysisCreatedAt = Now;

        var result = await Tools().AnalyzeCommonMistakesAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.StartsWith("A Common mistakes analysis of this assignment is already in progress", result);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static GradeAllResponse Response(int queued, int alreadyDrafted)
    {
        var job = Job.Group(JobType.GradeAssignment, JobPayloads.Write(new GradeAssignmentPayload("course-1", "a-4")), Now);
        return new GradeAllResponse(JobPayloads.ToResponse(job), queued, 15, alreadyDrafted, 0, 0, 0, []);
    }

    private sealed class Fakes : ISubmissionFetchService, IJobService
    {
        public List<(string CourseId, string AssignmentId)> Fetches { get; } = [];

        public List<bool> Regrade { get; } = [];

        public List<(string CourseId, string AssignmentId)> Analyses { get; } = [];

        public DateTimeOffset AnalysisCreatedAt { get; set; } = Now.AddMinutes(1);

        public JobResponse? AnalysisJob { get; private set; }

        public GradeAllResponse Result { get; set; } = new(null, 0, 0, 0, 0, 0, 0, []);

        public Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
        {
            Fetches.Add((courseId, assignmentId));
            return Task.FromResult(new FetchSummary(15, 0, 0, 15, 0, [], 1, false));
        }

        public Task<GradeAllResponse> QueueGradeAssignmentAsync(
            string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default)
        {
            Regrade.Add(regrade);
            return Task.FromResult(Result);
        }

        public Task<JobResponse?> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<JobResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<JobOverviewResponse> GetOverviewAsync(
            JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueGradeStudentAsync(string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
            Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems,
            bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
        {
            Analyses.Add((courseId, assignmentId));
            var job = new Job(JobType.AnalyzeMistakes, JobPayloads.Write(new AnalyzeMistakesPayload(courseId, assignmentId)), AnalysisCreatedAt,
                courseId: courseId, assignmentId: assignmentId);
            AnalysisJob = JobPayloads.ToResponse(job);
            return Task.FromResult(AnalysisJob);
        }

        public Task<JobResponse> QueueAnalyzeStudentAsync(
            string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
