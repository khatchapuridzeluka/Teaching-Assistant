using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class StudentAnalysisToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeAgentJobs _store = new();
    private readonly QueueingJobs _queue = new();
    private readonly AgentRunContext _context = new();

    private StudentAnalysisTools Tools()
    {
        return new StudentAnalysisTools(_store, _queue, _context, new FixedClock(Now));
    }

    [Fact]
    public void TheToolIsMarkedWithItsName()
    {
        var names = typeof(StudentAnalysisTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>();

        Assert.Equal(["analyze_student"], names);
    }

    [Fact]
    public async Task ACurrentSavedAnalysis_IsReturnedFree_AndNothingIsQueued()
    {
        _store.StudentAnalyses.Add(Analysis(StudentAnalysisKind.Strengths, outdated: false));

        var result = await Tools().AnalyzeStudentAsync("course-1", "s-1", "strengths", cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("The saved analysis is still current (nothing new was started):\nStrengths analysis from 4 strengths in 2 graded assignments", result);
        Assert.Contains("Overview: Strong in design.", result);
        Assert.Contains("- Clean class design (3 strengths in 2 assignments): Keeps classes focused.", result);
        Assert.DoesNotContain("Not grouped", result);
        Assert.Empty(_queue.Queued);
        Assert.Equal(AgentLinkKind.StudentStrengths, Assert.Single(_context.TakeLinks()).Kind);
        Assert.Null(_context.TakeQueuedJob());
    }

    [Fact]
    public async Task AnOutdatedOrMissingAnalysis_QueuesOnePaidRun_AndRecordsTheJob()
    {
        _store.StudentAnalyses.Add(Analysis(StudentAnalysisKind.Mistakes, outdated: true));

        var outdated = await Tools().AnalyzeStudentAsync("course-1", "s-1", "Mistakes", cancellationToken: TestContext.Current.CancellationToken);
        var missing = await Tools().AnalyzeStudentAsync("course-1", "s-1", "Overview", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Mistakes analysis queued (job id", outdated);
        Assert.Contains("The saved one was out of date: reports changed since.", outdated);
        Assert.Contains("Overview analysis queued (job id", missing);
        Assert.Contains("There was no saved one yet.", missing);
        Assert.Equal([StudentAnalysisKind.Mistakes, StudentAnalysisKind.Overview], _queue.Queued);
        Assert.NotNull(_context.TakeQueuedJob());
        Assert.NotNull(_context.TakeQueuedJob());
    }

    [Fact]
    public async Task Refresh_QueuesANewRun_EvenWhenTheSavedOneIsCurrent()
    {
        _store.StudentAnalyses.Add(Analysis(StudentAnalysisKind.Overview, outdated: false));

        var result = await Tools().AnalyzeStudentAsync("course-1", "s-1", "Overview", refresh: true, TestContext.Current.CancellationToken);

        Assert.Contains("Overview analysis queued", result);
        Assert.Equal([StudentAnalysisKind.Overview], _queue.Queued);
    }

    [Fact]
    public async Task AnUnknownKind_IsRefused()
    {
        var result = await Tools().AnalyzeStudentAsync("course-1", "s-1", "grades", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Unknown kind 'grades'. Use Mistakes, Strengths or Overview.", result);
        Assert.Empty(_queue.Queued);
    }

    [Fact]
    public async Task AFinishedAnalysisJob_IsReportedWithItsText_AndLinksToTheStudentsPage()
    {
        var analysis = Analysis(StudentAnalysisKind.Overview, outdated: false);
        _store.StudentAnalyses.Add(analysis);
        var job = new Job(JobType.AnalyzeStudent,
            JobPayloads.Write(new AnalyzeStudentPayload
            {
                CourseId = "course-1",
                StudentId = "s-1",
                StudentName = "Nino",
                Kind = StudentAnalysisKind.Overview,
            }), Now, courseId: "course-1");
        job.Start("worker", Now, TimeSpan.FromMinutes(10));
        job.Succeed(JobPayloads.Write(new AnalyzeStudentResult { AnalysisId = analysis.Id }), Now);
        var reporter = new JobReporter(_store, _store, _store, _store);

        var text = await reporter.DescribeAsync([job], TestContext.Current.CancellationToken);
        var link = Assert.Single(await reporter.LinksAsync([job], TestContext.Current.CancellationToken));

        Assert.Contains("The overview analysis of Nino is ready.", text);
        Assert.Contains("  Summary: Strong in design.", text);
        Assert.Contains("  What to work on next: Name things well.", text);
        Assert.Equal((AgentLinkKind.StudentOverview, "Nino's overview", "s-1"), (link.Kind, link.Label, link.StudentId));
    }

    private static StudentAnalysisResponse Analysis(StudentAnalysisKind kind, bool outdated)
    {
        var groups = kind == StudentAnalysisKind.Strengths
            ? new List<StrengthGroupResponse>
            {
                new()
                {
                    Title = "Clean class design",
                    Explanation = "Keeps classes focused.",
                    Ungrouped = false,
                    Count = 3,
                    Assignments = 2,
                    Categories = [],
                    Strengths = [],
                },
                new()
                {
                    Title = "Not grouped",
                    Explanation = "",
                    Ungrouped = true,
                    Count = 1,
                    Assignments = 1,
                    Categories = [],
                    Strengths = [],
                },
            }
            : [];
        return new StudentAnalysisResponse
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Model = "model",
            Summary = "Strong in design.",
            NextSteps = kind == StudentAnalysisKind.Strengths ? "" : "Name things well.",
            Assignments = 2,
            Records = 4,
            CreatedAt = Now.AddDays(-1),
            Outdated = outdated,
            Groups = groups,
        };
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class QueueingJobs : IJobService
    {
        public Task<TeachingAssistant.Application.Contracts.JobResponse> QueueEvaluationRunAsync(Guid runId, IReadOnlyList<Guid> resultIds, string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public List<StudentAnalysisKind> Queued { get; } = [];

        public Task<JobResponse> QueueAnalyzeStudentAsync(
            string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default)
        {
            Queued.Add(kind);
            var job = new Job(JobType.AnalyzeStudent, JobPayloads.Write(new AnalyzeStudentPayload
            {
                CourseId = courseId,
                StudentId = studentId,
                Kind = kind,
            }), Now,
                courseId: courseId);
            return Task.FromResult(JobPayloads.ToResponse(job));
        }

        public Task<JobResponse?> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<JobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<JobResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<JobOverviewResponse> GetOverviewAsync(
            JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<GradeAllResponse> QueueGradeAssignmentAsync(
            string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueGradeStudentAsync(
            string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
            Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems, bool regrade = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
