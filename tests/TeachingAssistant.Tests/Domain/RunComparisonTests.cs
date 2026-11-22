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
            new ReportMistakeEdit
            {
                SourceMistakeId = loop.Id,
                Rejected = false,
                Severity = loop.Severity,
                Category = loop.Category,
                Message = loop.Message,
                Points = loop.Points,
            },
            new ReportMistakeEdit
            {
                SourceMistakeId = naming.Id,
                Rejected = false,
                Severity = naming.Severity,
                Category = naming.Category,
                Message = naming.Message,
                Points = naming.Points,
            },
            new ReportMistakeEdit
            {
                SourceMistakeId = missing.Id,
                Rejected = false,
                Severity = missing.Severity,
                Category = missing.Category,
                Message = missing.Message,
                Points = missing.Points,
            },
            new ReportMistakeEdit
            {
                SourceMistakeId = comments.Id,
                Rejected = true,
                Severity = comments.Severity,
                Category = comments.Category,
                Message = comments.Message,
                Points = comments.Points,
            },
        ], Now);
        report.Approve(Now);

        var result = new EvaluationRunResult(Guid.NewGuid(), report.Id, report.VersionId, "claude-haiku-5-5", Now);
        result.Complete("claude-haiku-5-5", 90, 100, 2,
        [
            new RunMistake
            {
                File = "src\\Zoo.java",
                StartLine = 13,
                EndLine = 14,
                Severity = MistakeSeverity.Minor,
                Category = MistakeCategory.Correctness,
                Message = "Loop off by one.",
                Points = 5,
            },  // one line from 10-12: found
            new RunMistake
            {
                Severity = MistakeSeverity.Major,
                Category = MistakeCategory.MissingRequirement,
                Message = "No task 3.",
                Points = 10,
            },        // whole submission, same category: found
            new RunMistake
            {
                File = "src/Main.java",
                StartLine = 5,
                EndLine = 6,
                Severity = MistakeSeverity.Style,
                Category = MistakeCategory.Readability,
                Message = "Comment it.",
                Points = 0,
            },        // the rejected one: repeated
            new RunMistake
            {
                File = "src/Zoo.java",
                StartLine = 50,
                EndLine = 50,
                Severity = MistakeSeverity.Minor,
                Category = MistakeCategory.Design,
                Message = "Public field.",
                Points = 2,
            },          // nothing there: unreviewed
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
