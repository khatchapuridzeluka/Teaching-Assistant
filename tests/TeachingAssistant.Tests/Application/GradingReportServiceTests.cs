using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class GradingReportServiceTests
{
    private readonly FakeGradings _gradings = new();
    private readonly FakeReports _reports = new();
    private readonly FakeSubmissions _submissions = new();
    private readonly FakeStudentMistakes _studentMistakes = new();
    private readonly FakeStudentStrengths _studentStrengths = new();

    private GradingReportService Service() =>
        new(_gradings, _reports, _submissions, _studentMistakes, _studentStrengths, TimeProvider.System);

    [Fact]
    public async Task Get_IsNull_UntilTheFirstSave_ThenTheSameReportIsUpdated()
    {
        var grading = Create();
        Assert.Null(await Service().GetAsync(grading.Id, TestContext.Current.CancellationToken));

        var first = await Service().SaveAsync(grading.Id, Request(grading, 90), TestContext.Current.CancellationToken);
        var second = await Service().SaveAsync(grading.Id, Request(grading, 95), TestContext.Current.CancellationToken);

        Assert.Equal(first!.Id, second!.Id);
        Assert.Single(_reports.Reports);
        Assert.Equal(((double?)95, (double?)100, ReportStatus.InReview), (second.Score, second.MaxScore, second.Status));
        Assert.Equal(("student-1", "course-1", "assignment-1"), (second.StudentId, second.CourseId, second.AssignmentId));
        Assert.Empty(_studentMistakes.Mistakes);
    }

    [Fact]
    public async Task Approve_RecordsKeptAndEditedMistakesForTheStudent_NotRejectedOnes()
    {
        var grading = Create();

        var report = await Service().ApproveAsync(grading.Id, Request(grading, 88, rejectFirst: true), TestContext.Current.CancellationToken);

        Assert.Equal(ReportStatus.Approved, report!.Status);
        Assert.NotNull(report.ApprovedAt);
        Assert.Equal(MistakeDecision.Rejected, report.Mistakes[0].Decision);
        var recorded = Assert.Single(_studentMistakes.Mistakes);
        Assert.Equal(("student-1", "course-1", "assignment-1"), (recorded.StudentId, recorded.CourseId, recorded.AssignmentId));
        Assert.Equal((report.Id, report.Mistakes[1].Id, "Second"), (recorded.ReportId, recorded.ReportMistakeId, recorded.Message));
    }

    [Fact]
    public async Task ReopeningAnApprovedReport_RemovesItsRecords_ApprovingAgainWritesThemOnce()
    {
        var grading = Create();

        await Service().ApproveAsync(grading.Id, Request(grading, 88), TestContext.Current.CancellationToken);
        Assert.Equal(2, _studentMistakes.Mistakes.Count);

        await Service().SaveAsync(grading.Id, Request(grading, 90), TestContext.Current.CancellationToken);
        Assert.Empty(_studentMistakes.Mistakes);

        await Service().ApproveAsync(grading.Id, Request(grading, 90), TestContext.Current.CancellationToken);
        Assert.Equal(2, _studentMistakes.Mistakes.Count);
    }

    [Fact]
    public async Task Strengths_AreCopiedToTheReport_EditedOrRejected_AndOnlyKeptOnesAreRecordedOnApprove()
    {
        var grading = Create();
        var request = Request(grading, 88) with
        {
            Strengths =
            [
                new ReportStrengthInput(grading.Strengths[0].Id, true, MistakeCategory.Design, grading.Strengths[0].Message),
                new ReportStrengthInput(grading.Strengths[1].Id, false, MistakeCategory.ErrorHandling, "  Validates every input value. "),
            ],
        };

        var saved = await Service().SaveAsync(grading.Id, Request(grading, 88), TestContext.Current.CancellationToken);
        Assert.Equal(["Clean hierarchy", "Validates input"], saved!.Strengths.Select(s => s.Message));
        Assert.All(saved.Strengths, s => Assert.Equal(MistakeDecision.Kept, s.Decision));
        Assert.Empty(_studentStrengths.Strengths);

        var report = await Service().ApproveAsync(grading.Id, request, TestContext.Current.CancellationToken);

        Assert.Equal([MistakeDecision.Rejected, MistakeDecision.Edited], report!.Strengths.Select(s => s.Decision));
        var recorded = Assert.Single(_studentStrengths.Strengths);
        Assert.Equal(("student-1", "assignment-1", MistakeCategory.ErrorHandling, "Validates every input value."),
            (recorded.StudentId, recorded.AssignmentId, recorded.Category, recorded.Message));

        await Service().SaveAsync(grading.Id, request, TestContext.Current.CancellationToken);
        Assert.Empty(_studentStrengths.Strengths);
    }

    [Fact]
    public async Task ApprovingADraft_DiscardsTheStudentsOtherDraftsOfTheSubmission_AndOnlyItsRecordsCount()
    {
        var first = Create();
        var submission = _submissions.Submissions.Single();
        await Service().ApproveAsync(first.Id, Request(first, 80), TestContext.Current.CancellationToken);
        Assert.Equal(2, _studentMistakes.Mistakes.Count);

        var regrade = Add(first.VersionId, "Regrade mistake");
        var resubmission = submission.AddVersion(VersionStatus.Ready, null, "hash-2", "path-2", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);
        var latest = Add(resubmission.Id, "Latest mistake");

        await Service().ApproveAsync(latest.Id, Request(latest, 95), TestContext.Current.CancellationToken);

        Assert.Equal([GradingStatus.Discarded, GradingStatus.Discarded, GradingStatus.Draft], new[] { first, regrade, latest }.Select(g => g.Status));
        Assert.Equal(["Latest mistake"], _studentMistakes.Mistakes.Select(m => m.Message));
        Assert.Equal(["Latest strength"], _studentStrengths.Strengths.Select(s => s.Message));
        Assert.Equal(2, _reports.Reports.Count);
    }

    [Fact]
    public async Task DiscardingAnApprovedDraft_RemovesItsRecordsFromTheProfile()
    {
        var grading = Create();
        await Service().ApproveAsync(grading.Id, Request(grading, 80), TestContext.Current.CancellationToken);

        await new GradingDraftService(_gradings, _reports, _studentMistakes, _studentStrengths, TimeProvider.System)
            .DiscardAsync(grading.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GradingStatus.Discarded, grading.Status);
        Assert.Empty(_studentMistakes.Mistakes);
        Assert.Empty(_studentStrengths.Strengths);
    }

    [Fact]
    public async Task UnknownDraft_IsNull_BadInput_IsRejected()
    {
        var grading = Create();
        var unknownMistake = Request(grading, 80) with
        {
            Mistakes = [new ReportMistakeInput(Guid.NewGuid(), false, MistakeSeverity.Minor, MistakeCategory.Other, "x")],
        };

        Assert.Null(await Service().SaveAsync(Guid.NewGuid(), Request(grading, 80), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().SaveAsync(grading.Id, Request(grading, -1), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().SaveAsync(grading.Id, Request(grading, 80) with { Review = " " }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().SaveAsync(grading.Id, unknownMistake, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().SaveAsync(grading.Id, Request(grading, 80) with
            {
                Strengths = [new ReportStrengthInput(Guid.NewGuid(), false, MistakeCategory.Design, "x")],
            }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().SaveAsync(grading.Id, Request(grading, 80) with
            {
                Strengths = [new ReportStrengthInput(grading.Strengths[0].Id, false, MistakeCategory.Design, " ")],
            }, TestContext.Current.CancellationToken));
        Assert.Empty(_reports.Reports);
    }

    private AiGrading Create()
    {
        var now = DateTimeOffset.UtcNow;
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", "assignment-1", "student-1", "Ana", "sub-1", "folder", now);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", now, now, []);
        _submissions.Submissions.Add(submission);

        var grading = new AiGrading(version.Id, "model", "AI review", 82, 100, null, 1, [], [], "{}",
        [
            new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, MistakeCategory.Naming, "First"),
            new GradingMistake("A.java", 5, 6, MistakeSeverity.Major, MistakeCategory.Correctness, "Second"),
        ], now,
        [
            new GradingStrength("A.java", 10, 20, MistakeCategory.Design, "Clean hierarchy"),
            new GradingStrength(null, null, null, MistakeCategory.ErrorHandling, "Validates input"),
        ]);
        _gradings.Add(grading);
        return grading;
    }

    private AiGrading Add(Guid versionId, string message)
    {
        var grading = new AiGrading(versionId, "model", "AI review", 90, 100, null, 1, [], [], "{}",
            [new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, MistakeCategory.Naming, message)], DateTimeOffset.UtcNow,
            [new GradingStrength(null, null, null, MistakeCategory.Design, message.Replace("mistake", "strength"))]);
        _gradings.Add(grading);
        return grading;
    }

    private static SaveGradingReportRequest Request(AiGrading grading, double score, bool rejectFirst = false) =>
        new(score, null, "Teacher review",
            grading.Mistakes.Select((m, i) => new ReportMistakeInput(m.Id, rejectFirst && i == 0, m.Severity, m.Category, m.Message)).ToList());

    private sealed class FakeGradings : IGradingStore
    {
        private readonly List<AiGrading> _gradings = [];

        public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_gradings.FirstOrDefault(g => g.Id == gradingId));

        public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiGrading>>(_gradings.Where(g => versionIds.Contains(g.VersionId)).ToList());

        public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
            SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(AiGrading grading) => _gradings.Add(grading);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeReports : IGradingReportStore
    {
        public Task<IReadOnlyList<TeachingAssistant.Application.Models.ApprovedReport>> GetApprovedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public List<GradingReport> Reports { get; } = [];

        public Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Reports.FirstOrDefault(r => r.GradingId == gradingId));

        public Task<IReadOnlyList<ReportScore>> GetScoresAsync(IReadOnlyCollection<Guid> reportIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(GradingReport report) => Reports.Add(report);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStudentMistakes : IStudentMistakeStore
    {
        public List<StudentMistake> Mistakes { get; } = [];

        public Task<IReadOnlyList<StudentMistake>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentMistake>>(Mistakes.Where(m => m.ReportId == reportId).ToList());

        public Task<IReadOnlyList<StudentMistake>> GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentMistake>>(Mistakes.Where(m => m.CourseId == courseId && m.StudentId == studentId).ToList());

        public void AddRange(IEnumerable<StudentMistake> mistakes) => Mistakes.AddRange(mistakes);

        public void RemoveRange(IEnumerable<StudentMistake> mistakes)
        {
            foreach (var mistake in mistakes.ToList())
            {
                Mistakes.Remove(mistake);
            }
        }
    }

    private sealed class FakeStudentStrengths : IStudentStrengthStore
    {
        public List<StudentStrength> Strengths { get; } = [];

        public Task<IReadOnlyList<StudentStrength>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentStrength>>(Strengths.Where(s => s.ReportId == reportId).ToList());

        public Task<IReadOnlyList<StudentStrength>> GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentStrength>>(Strengths.Where(s => s.CourseId == courseId && s.StudentId == studentId).ToList());

        public void AddRange(IEnumerable<StudentStrength> strengths) => Strengths.AddRange(strengths);

        public void RemoveRange(IEnumerable<StudentStrength> strengths)
        {
            foreach (var strength in strengths.ToList())
            {
                Strengths.Remove(strength);
            }
        }
    }

    private sealed class FakeSubmissions : ISubmissionStore
    {
        public List<Submission> Submissions { get; } = [];

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Submissions.FirstOrDefault(s => s.Versions.Any(v => v.Id == versionId)));

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => Submissions.Add(submission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
