using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

public class ResultToolsTests
{
    private readonly FakeGradingStore _gradings = new();
    private readonly FakeReports _reports = new();
    private readonly FakeAnalyses _analyses = new();
    private readonly AgentRunContext _context = new();

    private ResultTools Tools()
    {
        return new ResultTools(_gradings, _reports, _analyses, _context);
    }

    [Fact]
    public void TheToolsAreMarked()
    {
        var names = typeof(ResultTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>()
            .Order();

        Assert.Equal(["get_common_mistakes", "get_grading"], names);
    }

    [Fact]
    public async Task GetGrading_WithoutAReport_ReadsTheAiDraft_MarkedAsNotReviewed_WithoutTheName()
    {
        _gradings.AddDraft(Draft(), "a-4", "external-1", "student-1", 2);

        var result = await Tools().GetGradingAsync("course-1", "a-4", "student-1", TestContext.Current.CancellationToken);

        Assert.StartsWith("AI draft of version 2, not reviewed by the teacher yet (model claude-opus-5-5). Score 72 / 100.", result);
        Assert.Contains("Review:\n**Score**: 72 / 100", result);
        Assert.Contains("- Major Correctness, -10 points (src/Zoo.java:12-14): The loop skips the last animal.", result);
        Assert.Contains("- Style Naming, -0 points (src/Zoo.java:3): Use camelCase.", result);
        Assert.Contains("Strengths:\n- Design (src/Animal.java:1-20): A clean abstract class.", result);
        Assert.DoesNotContain("Nino", result);
        Assert.Equal(AgentLinkKind.Submission, Assert.Single(_context.TakeLinks()).Kind);
    }

    [Fact]
    public async Task GetGrading_WithAReport_ReadsTheTeachersVersion_AndLeavesOutRejectedMistakes()
    {
        var draft = Draft();
        _gradings.AddDraft(draft, "a-4", "external-1", "student-1", 2, ReportStatus.Approved);
        _reports.Report = new GradingReportResponse
        {
            Id = Guid.NewGuid(),
            GradingId = draft.Id,
            VersionId = draft.VersionId,
            StudentId = "student-1",
            CourseId = "course-1",
            AssignmentId = "a-4",
            Status = ReportStatus.Approved,
            Score = 80,
            MaxScore = 100,
            Review = "The teacher's review",
            Mistakes =
            [
                ReportMistake("The loop skips the last animal; minor.", MistakeDecision.Edited, 4),
                ReportMistake("Use camelCase.", MistakeDecision.Rejected, 0),
            ],
            Strengths = [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var result = await Tools().GetGradingAsync("course-1", "a-4", "student-1", TestContext.Current.CancellationToken);

        Assert.StartsWith("The teacher's report of version 2, approved by the teacher. Score 80 / 100. The AI draft had 72.", result);
        Assert.Contains("The loop skips the last animal; minor. (edited by the teacher)", result);
        Assert.DoesNotContain("camelCase", result);
        Assert.Contains("The teacher rejected 1 of the AI's mistakes", result);
    }

    [Fact]
    public async Task GetGrading_WithoutADraft_OffersToGrade()
    {
        var result = await Tools().GetGradingAsync("course-1", "a-4", "student-1", TestContext.Current.CancellationToken);

        Assert.StartsWith("This student has no current grading for this assignment", result);
        Assert.Contains("grade_student_assignment", result);
        Assert.Empty(_context.TakeLinks());
    }

    [Fact]
    public async Task GetCommonMistakes_ReadsTheSavedAnalysis_WithoutNames()
    {
        var member = new ClusterMember(Guid.NewGuid(), Guid.NewGuid(), "student-1", "Nino", "external-1", MistakeSeverity.Major,
            MistakeCategory.Correctness, 5, "Off by one", "src/Zoo.java", 3, 4);
        _analyses.Latest = new MistakeAnalysis("course-1", "a-4", "model", "Loops are the main problem.", "Revisit loop bounds.", 1, 1,
            [new MistakeCluster("Loop bounds", "Explanation", false, [member])], new DateTimeOffset(2026, 11, 26, 9, 30, 0, TimeSpan.Zero));

        var result = await Tools().GetCommonMistakesAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.StartsWith("The latest Common mistakes analysis, made 2026-11-26 09:30 UTC", result);
        Assert.Contains("Overview: Loops are the main problem.", result);
        Assert.Contains("- Loop bounds: 1 student, 1 mistake", result);
        Assert.Contains("For the next lecture: Revisit loop bounds.", result);
        Assert.DoesNotContain("Nino", result);
        Assert.Equal(AgentLinkKind.CommonMistakes, Assert.Single(_context.TakeLinks()).Kind);
    }

    [Fact]
    public async Task GetCommonMistakes_WithoutAnAnalysis_PointsToThePaidOne()
    {
        var result = await Tools().GetCommonMistakesAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.StartsWith("No Common mistakes analysis of this assignment is saved yet.", result);
    }

    private static AiGrading Draft()
    {
        return new AiGrading(Guid.NewGuid(), "claude-opus-5-5", "**Score**: 72 / 100", 72, 100, null, 1, [], [], "{}",
            [
                new GradingMistake("src/Zoo.java", 12, 14, MistakeSeverity.Major, MistakeCategory.Correctness, "The loop skips the last animal.", 10),
                new GradingMistake("src/Zoo.java", 3, 3, MistakeSeverity.Style, MistakeCategory.Naming, "Use camelCase.", 0),
            ],
            DateTimeOffset.UtcNow,
            [new GradingStrength("src/Animal.java", 1, 20, MistakeCategory.Design, "A clean abstract class.")]);
    }

    private static ReportMistakeResponse ReportMistake(string message, MistakeDecision decision, double points)
    {
        return new ReportMistakeResponse
        {
            Id = Guid.NewGuid(),
            File = "src/Zoo.java",
            StartLine = 12,
            EndLine = 14,
            Severity = MistakeSeverity.Minor,
            Category = MistakeCategory.Correctness,
            Message = message,
            Points = points,
            Decision = decision,
        };
    }

    private sealed class FakeReports : IGradingReportService
    {
        public GradingReportResponse? Report { get; set; }

        public Task<GradingReportResponse?> GetAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Report?.GradingId == gradingId ? Report : null);

        public Task<GradingReportResponse?> SaveAsync(Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GradingReportResponse?> ApproveAsync(Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAnalyses : IMistakeAnalysisStore
    {
        public MistakeAnalysis? Latest { get; set; }

        public Task<MistakeAnalysis?> GetLatestAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Latest);

        public Task<MistakeAnalysis?> FindByClusterAsync(Guid clusterId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(MistakeAnalysis analysis) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
