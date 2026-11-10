using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Domain;

public class StrengthHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<string, DateTimeOffset?> _dueDates = new()
    {
        ["a1"] = Now.AddDays(-30),
        ["a2"] = Now.AddDays(-20),
        ["a3"] = Now.AddDays(-10),
    };

    [Fact]
    public void AStrengthAfterAnEarlierStrength_IsConsistent_AfterAnEarlierMistake_IsImproved_OtherwiseNew()
    {
        var a1 = Approve("a1", strengths: [MistakeCategory.Design], mistakes: [MistakeCategory.ErrorHandling]);
        var a2 = Approve("a2", strengths: [MistakeCategory.Design, MistakeCategory.ErrorHandling, MistakeCategory.Naming], mistakes: []);

        var labeled = StrengthHistory.Label(a1.Strengths.Concat(a2.Strengths), a1.Mistakes.Concat(a2.Mistakes), DueDate)
            .ToDictionary(l => (l.Strength.AssignmentId, l.Strength.Category));

        Assert.Equal(StrengthLabel.New, labeled[("a1", MistakeCategory.Design)].Label);
        Assert.Equal((StrengthLabel.Consistent, "a1"), (labeled[("a2", MistakeCategory.Design)].Label, labeled[("a2", MistakeCategory.Design)].SinceAssignmentId));
        Assert.Equal((StrengthLabel.Improved, "a1"),
            (labeled[("a2", MistakeCategory.ErrorHandling)].Label, labeled[("a2", MistakeCategory.ErrorHandling)].SinceAssignmentId));
        Assert.Equal(StrengthLabel.New, labeled[("a2", MistakeCategory.Naming)].Label);
    }

    [Fact]
    public void AMistakeInTheSameOrALaterAssignment_DoesNotMakeAStrengthImproved()
    {
        var a1 = Approve("a1", strengths: [MistakeCategory.Design], mistakes: [MistakeCategory.Design]);
        var a3 = Approve("a3", strengths: [], mistakes: [MistakeCategory.Design]);

        var labeled = StrengthHistory.Label(a1.Strengths, a1.Mistakes.Concat(a3.Mistakes), DueDate);

        Assert.Equal(StrengthLabel.New, Assert.Single(labeled).Label);
    }

    [Fact]
    public void Trends_PutCategoriesShownInMostAssignmentsFirst_CountMistakes_AndNameWhereItImprovedFrom()
    {
        var a1 = Approve("a1", strengths: [MistakeCategory.Naming], mistakes: [MistakeCategory.ErrorHandling, MistakeCategory.ErrorHandling]);
        var a2 = Approve("a2", strengths: [MistakeCategory.ErrorHandling], mistakes: []);
        var a3 = Approve("a3", strengths: [MistakeCategory.ErrorHandling], mistakes: []);

        var trends = StrengthHistory.Trends(a1.Strengths.Concat(a2.Strengths).Concat(a3.Strengths), a1.Mistakes, DueDate);

        Assert.Equal([MistakeCategory.ErrorHandling, MistakeCategory.Naming], trends.Select(t => t.Category));
        Assert.Equal((2, 2, true, 2, "a1", "a2", "a3"), (trends[0].Count, trends[0].Assignments, trends[0].Consistent, trends[0].Mistakes,
            trends[0].ImprovedSinceAssignmentId, trends[0].FirstAssignmentId, trends[0].LastAssignmentId));
        Assert.Equal(((string?)null, false), (trends[1].ImprovedSinceAssignmentId, trends[1].Consistent));
    }

    [Fact]
    public void RejectedStrengths_AreNotRecorded()
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", "a1", "student", "Ana", "sub-a1", "folder", Now);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []);
        var grading = new AiGrading(version.Id, "model", "review", 80, 100, null, 1, [], [], "{}", [], Now,
            [new GradingStrength(null, null, null, MistakeCategory.Design, "a"), new GradingStrength(null, null, null, MistakeCategory.Naming, "b")]);
        var report = new GradingReport(grading, submission, Now);
        report.Save(grading, 80, null, "review", [], Now,
            [new ReportStrengthEdit(grading.Strengths[0].Id, true, MistakeCategory.Design, "a")]);
        report.Approve(Now);

        var recorded = Assert.Single(StudentStrength.FromApprovedReport(report, submission));
        Assert.Equal(MistakeCategory.Naming, recorded.Category);
    }

    private DateTimeOffset? DueDate(string assignmentId) => _dueDates.GetValueOrDefault(assignmentId);

    private static (IReadOnlyList<StudentStrength> Strengths, IReadOnlyList<StudentMistake> Mistakes) Approve(
        string assignmentId, MistakeCategory[] strengths, MistakeCategory[] mistakes)
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", assignmentId, "student", "Ana", $"sub-{assignmentId}", "folder", Now);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []);
        var grading = new AiGrading(version.Id, "model", "review", 80, 100, null, 1, [], [], "{}",
            mistakes.Select(c => new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, c, $"{c} problem", 1)), Now,
            strengths.Select(c => new GradingStrength("A.java", 2, 2, c, $"{c} done well")));
        var report = new GradingReport(grading, submission, Now);
        report.Approve(Now);
        return (StudentStrength.FromApprovedReport(report, submission), StudentMistake.FromApprovedReport(report, submission));
    }
}
