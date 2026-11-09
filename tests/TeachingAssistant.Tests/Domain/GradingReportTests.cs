using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Domain;

public class GradingReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly Submission _submission;
    private readonly Guid _versionId;

    public GradingReportTests()
    {
        _submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", "assignment-1", "student-1", "Ana", "sub-1", "folder", Now);
        _versionId = _submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []).Id;
    }

    private AiGrading Grading() => new(_versionId, "model", "AI review", 82, 100, 7, 1, [], [], "{}",
        [
            new GradingMistake("Animal.java", 12, 14, MistakeSeverity.Minor, MistakeCategory.Naming, "Field costs should be foodCosts"),
            new GradingMistake("Zoo.java", 40, 40, MistakeSeverity.Style, MistakeCategory.Readability, "Unused import"),
        ],
        Now);

    [Fact]
    public void NewReport_StartsAsACopyOfTheDraft_InReview()
    {
        var grading = Grading();

        var report = new GradingReport(grading, _submission, Now);

        Assert.Equal((ReportStatus.InReview, (double?)82, (double?)7, "AI review"), (report.Status, report.Score, report.Bonus, report.Review));
        Assert.All(report.Mistakes, m => Assert.Equal(MistakeDecision.Kept, m.Decision));
        Assert.Equal(grading.Mistakes.Select(m => m.Id), report.Mistakes.Select(m => m.SourceMistakeId!.Value));
        Assert.Equal(("student-1", "course-1", "assignment-1"), (report.StudentId, report.CourseId, report.AssignmentId));
    }

    [Fact]
    public void NewReport_RefusesASubmissionTheDraftIsNotAbout()
    {
        var other = new Submission(SubmissionSource.GoogleClassroom, "course-1", "assignment-1", "student-2", "Beka", "sub-2", "folder", Now);

        Assert.Throws<ArgumentException>(() => new GradingReport(Grading(), other, Now));
    }

    [Fact]
    public void Save_RecordsKeptEditedAndRejected_AndNeverChangesTheDraft()
    {
        var grading = Grading();
        var report = new GradingReport(grading, _submission, Now);
        var naming = grading.Mistakes[0];
        var unused = grading.Mistakes[1];

        report.Save(grading, 88, 5, "My review",
        [
            new ReportMistakeEdit(naming.Id, false, MistakeSeverity.Major, naming.Category, naming.Message),
            new ReportMistakeEdit(unused.Id, true, unused.Severity, unused.Category, unused.Message),
        ], Now.AddMinutes(5));

        Assert.Equal([MistakeDecision.Edited, MistakeDecision.Rejected], report.Mistakes.Select(m => m.Decision));
        Assert.Equal(MistakeSeverity.Major, report.Mistakes[0].Severity);
        Assert.Equal(((double?)88, (double?)5, "My review"), (report.Score, report.Bonus, report.Review));
        Assert.Equal(((double?)82, MistakeSeverity.Minor, "AI review"), (grading.Score, naming.Severity, grading.Review));
    }

    [Fact]
    public void ChangingOnlyThePoints_CountsAsEdited_AndReachesTheStudentRecord()
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", "assignment", "student", "Ana", "sub", "folder", Now);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []);
        var grading = new AiGrading(version.Id, "model", "AI review", 82, 100, null, 1, [], [], "{}",
            [new GradingMistake(null, null, null, MistakeSeverity.Minor, MistakeCategory.Design, "getName() belongs in Creature", 1)], Now);
        var mistake = grading.Mistakes[0];
        var report = new GradingReport(grading, submission, Now);
        Assert.Equal(1, report.Mistakes[0].Points);

        report.Save(grading, 80, null, "Review",
            [new ReportMistakeEdit(mistake.Id, false, mistake.Severity, mistake.Category, mistake.Message, 3)], Now);
        report.Approve(Now);

        Assert.Equal((MistakeDecision.Edited, (double?)3), (report.Mistakes[0].Decision, report.Mistakes[0].Points));
        Assert.Equal((double?)1, mistake.Points);
        Assert.Equal((double?)3, Assert.Single(StudentMistake.FromApprovedReport(report, submission)).Points);
    }

    [Fact]
    public void NegativePoints_AreRefused()
    {
        var grading = Grading();
        var report = new GradingReport(grading, _submission, Now);
        var mistake = grading.Mistakes[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => report.Save(grading, 80, null, "Review",
            [new ReportMistakeEdit(mistake.Id, false, mistake.Severity, mistake.Category, mistake.Message, -2)], Now));
    }

    [Fact]
    public void EditingBackToTheOriginal_CountsAsKept()
    {
        var grading = Grading();
        var report = new GradingReport(grading, _submission, Now);
        var naming = grading.Mistakes[0];

        report.Save(grading, 82, 7, "AI review",
            [new ReportMistakeEdit(naming.Id, false, naming.Severity, naming.Category, "  " + naming.Message + " ")], Now);

        Assert.Equal(MistakeDecision.Kept, report.Mistakes[0].Decision);
    }

    [Fact]
    public void Approve_ThenSaveAgain_ReopensTheReport()
    {
        var grading = Grading();
        var report = new GradingReport(grading, _submission, Now);

        report.Approve(Now.AddMinutes(1));
        Assert.Equal((ReportStatus.Approved, (DateTimeOffset?)Now.AddMinutes(1)), (report.Status, report.ApprovedAt));

        report.Save(grading, 90, null, "Changed my mind", [], Now.AddMinutes(2));
        Assert.Equal((ReportStatus.InReview, (DateTimeOffset?)null), (report.Status, report.ApprovedAt));
    }

    [Fact]
    public void StudentMistakes_AreOnlyMadeFromAnApprovedReport()
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", "assignment", "student", "Ana", "sub", "folder", Now);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []);
        var grading = new AiGrading(version.Id, "model", "AI review", 82, 100, null, 1, [], [], "{}",
            [new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, MistakeCategory.Naming, "Bad name")], Now);
        var report = new GradingReport(grading, submission, Now);

        Assert.Throws<InvalidOperationException>(() => StudentMistake.FromApprovedReport(report, submission));

        report.Approve(Now);
        var recorded = Assert.Single(StudentMistake.FromApprovedReport(report, submission));
        Assert.Equal(("student", MistakeCategory.Naming, Now), (recorded.StudentId, recorded.Category, recorded.ApprovedAt));
    }

    [Fact]
    public void DiscardedDraft_CannotBeReviewed()
    {
        var grading = Grading();
        grading.Discard(Now);

        Assert.Throws<InvalidOperationException>(() => new GradingReport(grading, _submission, Now));
    }
}
