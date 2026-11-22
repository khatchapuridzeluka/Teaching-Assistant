using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Domain;

public class DraftComparisonTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComparesTheTotals_AndEveryDecision_AgainstWhatTheAiWrote()
    {
        var (draft, report) = DraftAndReport(score: 85, bonus: 5, max: 100,
            new GradingMistake("Zoo.java", 3, 3, MistakeSeverity.Minor, MistakeCategory.Naming, "Name 'count'.", 3),
            new GradingMistake("Zoo.java", 9, 9, MistakeSeverity.Major, MistakeCategory.Correctness, "Wrong loop.", 10),
            new GradingMistake(null, null, null, MistakeSeverity.Style, MistakeCategory.Readability, "Comments.", 0));
        var naming = draft.Mistakes[0];
        var loop = draft.Mistakes[1];
        var comments = draft.Mistakes[2];
        var strength = draft.Strengths[0];

        // The teacher keeps the naming mistake but lowers it to 1 point and calls it Encapsulation, rejects the loop mistake,
        // keeps the style remark as it is, rejects the strength, and gives 80 with no bonus.
        report.Save(draft, 80, null, "My review",
        [
            new ReportMistakeEdit
            {
                SourceMistakeId = naming.Id,
                Rejected = false,
                Severity = MistakeSeverity.Minor,
                Category = MistakeCategory.Encapsulation,
                Message = "Name 'count'.",
                Points = 1,
            },
            new ReportMistakeEdit
            {
                SourceMistakeId = loop.Id,
                Rejected = true,
                Severity = loop.Severity,
                Category = loop.Category,
                Message = loop.Message,
                Points = loop.Points,
            },
            new ReportMistakeEdit
            {
                SourceMistakeId = comments.Id,
                Rejected = false,
                Severity = comments.Severity,
                Category = comments.Category,
                Message = comments.Message,
                Points = comments.Points,
            },
        ], Now, [new ReportStrengthEdit
        {
            SourceStrengthId = strength.Id,
            Rejected = true,
            Category = strength.Category,
            Message = strength.Message,
        }]);
        report.Approve(Now);

        var comparison = DraftComparison.Of(draft, report);

        Assert.Equal((90.0, 80.0, 10.0, 10.0), (comparison.AiTotal!.Value, comparison.TeacherTotal!.Value,
            comparison.Difference!.Value, comparison.DifferencePercent!.Value));
        Assert.Equal((false, true), (comparison.Agrees(5), comparison.Agrees(10)));
        Assert.Equal(
            [
                new MistakeComparison
                {
                    Severity = MistakeSeverity.Minor,
                    Category = MistakeCategory.Naming,
                    Decision = MistakeDecision.Edited,
                    AiPoints = 3,
                    TeacherPoints = 1,
                },
                new MistakeComparison
                {
                    Severity = MistakeSeverity.Major,
                    Category = MistakeCategory.Correctness,
                    Decision = MistakeDecision.Rejected,
                    AiPoints = 10,
                    TeacherPoints = 0,
                },
                new MistakeComparison
                {
                    Severity = MistakeSeverity.Style,
                    Category = MistakeCategory.Readability,
                    Decision = MistakeDecision.Kept,
                    AiPoints = 0,
                    TeacherPoints = 0,
                },
            ],
            comparison.Mistakes);
        Assert.Equal([true, false, false], comparison.Mistakes.Select(m => m.PointsChanged));
        Assert.Equal((0, 0, 1), (comparison.StrengthsKept, comparison.StrengthsEdited, comparison.StrengthsRejected));
    }

    [Fact]
    public void ScoresOutOf50_CompareAsPercentOfTheMaximum_AndAMissingScoreHasNoDifference()
    {
        var (draft, report) = DraftAndReport(score: 40, bonus: null, max: 50);
        report.Save(draft, 45, null, "Review", [], Now);

        var comparison = DraftComparison.Of(draft, report);
        Assert.Equal((-10.0, 80.0, 90.0), (comparison.DifferencePercent!.Value, comparison.AiPercent!.Value, comparison.TeacherPercent!.Value));

        var (unscored, unscoredReport) = DraftAndReport(score: null, bonus: null, max: 100);
        Assert.Null(DraftComparison.Of(unscored, unscoredReport).Difference);
        Assert.Null(DraftComparison.Of(unscored, unscoredReport).Agrees(5));
    }

    [Fact]
    public void AReportOfAnotherDraft_IsRefused()
    {
        var (draft, _) = DraftAndReport(score: 80, bonus: null, max: 100);
        var (_, otherReport) = DraftAndReport(score: 80, bonus: null, max: 100);

        Assert.Throws<ArgumentException>(() => DraftComparison.Of(draft, otherReport));
    }

    internal static (AiGrading Draft, GradingReport Report) DraftAndReport(
        double? score, double? bonus, double? max, params GradingMistake[] mistakes)
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", "a-1", "student-1", "Nini", "sub-1", "folder", Now);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, Now, []);
        var draft = new AiGrading(version.Id, "claude-opus-5-5", "AI review", score, max, bonus, 1, [], [], "{}", mistakes, Now,
            [new GradingStrength("Zoo.java", 1, 2, MistakeCategory.Design, "Clean hierarchy.")]);
        return (draft, new GradingReport(draft, submission, Now));
    }
}
