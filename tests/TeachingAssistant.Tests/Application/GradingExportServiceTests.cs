using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class GradingExportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private readonly Fakes _fakes = new();

    private GradingExportService Service() => new(_fakes, _fakes, _fakes, _fakes, _fakes, TimeProvider.System);

    [Fact]
    public async Task ADraftWithoutReport_IsExportedAsTheAiWroteIt_MarkedAsNotReviewed()
    {
        var grading = _fakes.Graded();

        var file = await Service().ExportPdfAsync(grading.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(file);
        Assert.Equal(("application/pdf", "Assignment 2_ At the Zoo - Nini Beridze - v1.pdf"), (file.ContentType, file.FileName));
        var document = _fakes.Rendered!;
        Assert.Equal(GradingDocumentStatus.Draft, document.Status);
        Assert.Equal(((double?)85, (double?)100, (double?)5), (document.Score, document.MaxScore, document.Bonus));
        Assert.Equal(["Field is public", "Name differs"], document.Mistakes.Select(m => m.Message));
        Assert.Equal("Clean toString", Assert.Single(document.Strengths).Message);
        Assert.Equal("AI review", document.Review);
    }

    [Fact]
    public async Task AReviewedDraft_UsesTheTeachersReport_LeavesRejectedMistakesOut_AndKeepsTheAiScoreForComparison()
    {
        var grading = _fakes.Graded();
        var report = _fakes.Review(grading);
        var rejected = grading.Mistakes.Single(m => m.Message == "Name differs");
        report.Save(grading, 90, null, "Teacher review",
            [new ReportMistakeEdit(rejected.Id, true, rejected.Severity, rejected.Category, rejected.Message, rejected.Points)], Now);
        report.Approve(Now);

        await Service().ExportPdfAsync(grading.Id, TestContext.Current.CancellationToken);

        var document = _fakes.Rendered!;
        Assert.Equal(GradingDocumentStatus.Approved, document.Status);
        Assert.Equal(((double?)90, (double?)null, (double?)85, (double?)5), (document.Score, document.Bonus, document.AiScore, document.AiBonus));
        Assert.Equal("Teacher review", document.Review);
        Assert.Equal("Field is public", Assert.Single(document.Mistakes).Message);
        Assert.Equal(Now, document.ApprovedAt);
    }

    [Fact]
    public async Task ADiscardedDraft_IsMarkedDiscarded_AndAnUnknownIdIsNotFound()
    {
        var grading = _fakes.Graded();
        grading.Discard(Now);

        await Service().ExportPdfAsync(grading.Id, TestContext.Current.CancellationToken);

        Assert.Equal(GradingDocumentStatus.Discarded, _fakes.Rendered!.Status);
        Assert.Null(await Service().ExportPdfAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    private sealed class Fakes : IGradingStore, IGradingReportStore, ISubmissionStore, ICourseAssignmentStore, IGradingDocumentRenderer
    {
        public Task<IReadOnlyList<TeachingAssistant.Application.Models.ApprovedReport>> GetApprovedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private readonly List<AiGrading> _gradings = [];
        private readonly List<GradingReport> _reports = [];
        private readonly List<Submission> _submissions = [];

        public GradingDocument? Rendered { get; private set; }

        public AiGrading Graded()
        {
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course", "a-2", "s-1", "Nini Beridze", "sub-1", "folder", Now);
            var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []);
            var grading = new AiGrading(version.Id, "model", "AI review", 85, 100, 5, 1, [], [], "{}",
                [
                    new GradingMistake("Zoo.java", 3, 3, MistakeSeverity.Major, MistakeCategory.Encapsulation, "Field is public", 5),
                    new GradingMistake("Zoo.java", 9, 12, MistakeSeverity.Minor, MistakeCategory.Naming, "Name differs", 3),
                ],
                Now,
                [new GradingStrength("Animal.java", 20, 24, MistakeCategory.Design, "Clean toString")]);
            _submissions.Add(submission);
            _gradings.Add(grading);
            return grading;
        }

        public GradingReport Review(AiGrading grading)
        {
            var submission = _submissions.Single(s => s.Versions.Any(v => v.Id == grading.VersionId));
            var report = new GradingReport(grading, submission, Now);
            _reports.Add(report);
            return report;
        }

        public byte[] RenderPdf(GradingDocument document)
        {
            Rendered = document;
            return [0x25, 0x50, 0x44, 0x46];
        }

        public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_gradings.FirstOrDefault(g => g.Id == gradingId));

        public Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_reports.FirstOrDefault(r => r.GradingId == gradingId));

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_submissions.FirstOrDefault(s => s.Versions.Any(v => v.Id == versionId)));

        public Task<IReadOnlyList<CourseAssignment>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CourseAssignment>>(
                [new CourseAssignment(SubmissionSource.GoogleClassroom, "course", "a-2", "Assignment 2: At the Zoo", 100, Now.AddDays(-3), Now)]);

        Task<IReadOnlyList<Submission>> ISubmissionStore.GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
            SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(AiGrading grading) => throw new NotSupportedException();

        public Task<IReadOnlyList<ReportScore>> GetScoresAsync(IReadOnlyCollection<Guid> reportIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(GradingReport report) => throw new NotSupportedException();

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => throw new NotSupportedException();

        public void Add(CourseAssignment assignment) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
