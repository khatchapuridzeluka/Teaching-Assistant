using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Domain;

public class RunComparisonTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MistakesAreMatchedByPlace_AsFound_RepeatedFalseAlarms_OrUnreviewed()
    {
        var (draft, report) = DraftComparisonTests.DraftAndReport(score: 85, bonus: null, max: 100,
            new GradingMistake("src/Zoo.java", 10, 12, MistakeSeverity.Major, MistakeCategory.Correctness, "Wrong loop.", 10),
            new GradingMistake("src/Zoo.java", 30, 30, MistakeSeverity.Minor, MistakeCategory.Naming, "Name.", 3),
            new GradingMistake(null, null, null, MistakeSeverity.Major, MistakeCategory.MissingRequirement, "Task 3 missing.", 10),
            new GradingMistake("src/Main.java", 5, 5, MistakeSeverity.Style, MistakeCategory.Readability, "Comments.", 0));
        var loop = draft.Mistakes[0];
        var naming = draft.Mistakes[1];
        var missing = draft.Mistakes[2];
        var comments = draft.Mistakes[3];
        report.Save(draft, 80, null, "Review",
        [
            new ReportMistakeEdit(loop.Id, false, loop.Severity, loop.Category, loop.Message, loop.Points),
            new ReportMistakeEdit(naming.Id, false, naming.Severity, naming.Category, naming.Message, naming.Points),
            new ReportMistakeEdit(missing.Id, false, missing.Severity, missing.Category, missing.Message, missing.Points),
            new ReportMistakeEdit(comments.Id, true, comments.Severity, comments.Category, comments.Message, comments.Points),
        ], Now);
        report.Approve(Now);

        var result = new EvaluationRunResult(Guid.NewGuid(), report.Id, report.VersionId, "claude-haiku-5-5", Now);
        result.Complete("claude-haiku-5-5", 90, 100, 2,
        [
            new RunMistake("src\\Zoo.java", 13, 14, MistakeSeverity.Minor, MistakeCategory.Correctness, "Loop off by one.", 5),  // one line from 10-12: found
            new RunMistake(null, null, null, MistakeSeverity.Major, MistakeCategory.MissingRequirement, "No task 3.", 10),        // whole submission, same category: found
            new RunMistake("src/Main.java", 5, 6, MistakeSeverity.Style, MistakeCategory.Readability, "Comment it.", 0),        // the rejected one: repeated
            new RunMistake("src/Zoo.java", 50, 50, MistakeSeverity.Minor, MistakeCategory.Design, "Public field.", 2),          // nothing there: unreviewed
        ], BuildStatus.Compiled, TimeSpan.FromSeconds(40), Now);

        var comparison = RunComparison.Of(result, report, draft.MaxScore);

        Assert.Equal((92.0, 80.0, 12.0), (comparison.ModelTotal!.Value, comparison.TeacherTotal!.Value, comparison.DifferencePercent!.Value));
        Assert.Equal((3, 2, 1, 1, 1), (comparison.TeacherMistakes, comparison.Found, comparison.Missed, comparison.RepeatedFalseAlarms,
            comparison.Unreviewed));
        Assert.Equal((92.0, 80.0), (comparison.ModelPercent!.Value, comparison.TeacherPercent!.Value));

        // What it matched, as the model wrote it: the found loop with the teacher's points, the repeated one at 0; the unreviewed one is left out.
        Assert.Equal(
        [
            (MistakeCategory.Correctness, MistakeDecision.Kept, (double?)5, (double?)10),
            (MistakeCategory.MissingRequirement, MistakeDecision.Kept, 10, 10),
            (MistakeCategory.Readability, MistakeDecision.Rejected, 0, 0),
        ], comparison.Decisions.Select(d => (d.Category, d.Decision, d.AiPoints, d.TeacherPoints)));
        // Each mistake the teacher kept, and whether it was found: the naming one was missed.
        Assert.Equal(
        [
            (MistakeCategory.Correctness, true),
            (MistakeCategory.Naming, false),
            (MistakeCategory.MissingRequirement, true),
        ], comparison.TeacherResults.Select(r => (r.Category, r.Found)));
    }

    [Fact]
    public void AResultOfAnotherReport_IsRefused()
    {
        var (_, report) = DraftComparisonTests.DraftAndReport(score: 80, bonus: null, max: 100);
        var result = new EvaluationRunResult(Guid.NewGuid(), Guid.NewGuid(), report.VersionId, "claude-opus-5-5", Now);

        Assert.Throws<ArgumentException>(() => RunComparison.Of(result, report, 100));
    }

    [Fact]
    public void AFailedResult_KeepsItsErrorShortened_AndADoneResultClearsIt()
    {
        var result = new EvaluationRunResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "claude-haiku-5-5", Now);

        result.Fail(new string('x', EvaluationRunResult.MaxErrorLength + 50), Now);
        Assert.Equal((RunResultStatus.Failed, EvaluationRunResult.MaxErrorLength), (result.Status, result.Error!.Length));

        result.Complete("claude-haiku-5-5", 70, 100, null, [], BuildStatus.Skipped, TimeSpan.FromMilliseconds(1500), Now);
        Assert.Equal((RunResultStatus.Done, (string?)null, (int?)1500), (result.Status, result.Error, result.DurationMs));
    }
}
