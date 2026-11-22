using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
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
        return new GradingTools(_fakes, _fakes, _fakes, _context, new Clock(Now.AddMinutes(1)));
    }

    [Fact]
    public void TheToolsAreMarked()
    {
        var names = typeof(GradingTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>()
            .Order();

        Assert.Equal(["analyze_common_mistakes", "export_grading_pdf", "grade_all_students"], names);
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
        Assert.Equal(AgentLinkKind.Assignment, Assert.Single(_context.TakeLinks()).Kind);
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
        _fakes.Result = new GradeAllResponse
        {
            Queued = 0,
            Students = 15,
            AlreadyDrafted = 15,
            AlreadyQueued = 0,
            NoWork = 0,
            NotFetched = 0,
            Problems = [],
        };

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
        Assert.Equal([new AgentLink
        {
            Kind = AgentLinkKind.CommonMistakes,
            Label = "Common mistakes",
            CourseId = "course-1",
            AssignmentId = "a-4",
        }], _context.TakeLinks());
    }

    [Fact]
    public async Task ExportGradingPdf_AttachesADownloadLinkForTheCurrentDraft()
    {
        var result = await Tools().ExportGradingPdfAsync("course-1", "a-4", "s-1", TestContext.Current.CancellationToken);

        Assert.StartsWith("The PDF report of Nino's grading (version 1, score 80 / 100) is ready", result);
        var link = Assert.Single(_context.TakeLinks());
        Assert.Equal((AgentLinkKind.GradingPdf, "PDF · Nino", "s-1"), (link.Kind, link.Label, link.StudentId));
        Assert.NotNull(link.GradingId);
        Assert.Null(_context.TakeQueuedJob());
    }

    [Fact]
    public async Task ExportGradingPdf_SaysToGradeFirst_WhenThereIsNoCurrentDraft()
    {
        _fakes.Drafts.Clear();
        _fakes.Drafts.Add(GradingStatus.Discarded);

        var result = await Tools().ExportGradingPdfAsync("course-1", "a-4", "s-1", TestContext.Current.CancellationToken);

        Assert.StartsWith("No PDF: this student has no current AI draft", result);
        Assert.Empty(_context.TakeLinks());
    }

    [Fact]
    public async Task AnalyzeCommonMistakes_RefusesAtOnce_WhenEveryDraftWasDiscarded()
    {
        _fakes.Drafts.Clear();
        _fakes.Drafts.Add(GradingStatus.Discarded);

        var result = await Tools().AnalyzeCommonMistakesAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.StartsWith("Not started: no student of this assignment has a current AI draft", result);
        Assert.Contains("Offer the teacher to grade them first and then run the analysis", result);
        Assert.Empty(_fakes.Analyses);
        Assert.Null(_context.TakeQueuedJob());
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
        var job = Job.Group(JobType.GradeAssignment, JobPayloads.Write(new GradeAssignmentPayload
        {
            CourseId = "course-1",
            AssignmentId = "a-4",
        }), Now);
        return new GradeAllResponse
        {
            Job = JobPayloads.ToResponse(job),
            Queued = queued,
            Students = 15,
            AlreadyDrafted = alreadyDrafted,
            AlreadyQueued = 0,
            NoWork = 0,
            NotFetched = 0,
            Problems = [],
        };
    }

    private sealed class Fakes : ISubmissionFetchService, IJobService, IGradingStore
    {
        public Task<TeachingAssistant.Application.Contracts.JobResponse> QueueEvaluationRunAsync(Guid runId, IReadOnlyList<Guid> resultIds, string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TeachingAssistant.Application.Models.DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public List<GradingStatus> Drafts { get; } = [GradingStatus.Draft];

        public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
            SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GradingSummary>>(Drafts
                .Select(status => new GradingSummary
                {
                    Id = Guid.NewGuid(),
                    VersionId = Guid.NewGuid(),
                    VersionNumber = 1,
                    AssignmentId = "a-4",
                    SubmissionId = "sub",
                    StudentId = "s-1",
                    StudentName = "Nino",
                    Status = status,
                    Score = 80,
                    MaxScore = 100,
                    Major = 0,
                    Minor = 0,
                    Style = 0,
                    Model = "model",
                    CreatedAt = Now,
                })
                .ToList());

        public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(AiGrading grading) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public List<(string CourseId, string AssignmentId)> Fetches { get; } = [];

        public List<bool> Regrade { get; } = [];

        public List<(string CourseId, string AssignmentId)> Analyses { get; } = [];

        public DateTimeOffset AnalysisCreatedAt { get; set; } = Now.AddMinutes(1);

        public JobResponse? AnalysisJob { get; private set; }

        public GradeAllResponse Result { get; set; } = new()
        {
            Queued = 0,
            Students = 0,
            AlreadyDrafted = 0,
            AlreadyQueued = 0,
            NoWork = 0,
            NotFetched = 0,
            Problems = [],
        };

        public Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
        {
            Fetches.Add((courseId, assignmentId));
            return Task.FromResult(new FetchSummary
            {
                Submissions = 15,
                NewSubmissions = 0,
                NewVersions = 0,
                Unchanged = 15,
                NotSubmitted = 0,
                Problems = [],
                InstructionsVersion = 1,
                NewInstructions = false,
            });
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
            var job = new Job(JobType.AnalyzeMistakes, JobPayloads.Write(new AnalyzeMistakesPayload
            {
                CourseId = courseId,
                AssignmentId = assignmentId,
            }), AnalysisCreatedAt,
                courseId: courseId, assignmentId: assignmentId);
            AnalysisJob = JobPayloads.ToResponse(job);
            return Task.FromResult(AnalysisJob);
        }

        public Task<JobResponse> QueueAnalyzeStudentAsync(
            string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
