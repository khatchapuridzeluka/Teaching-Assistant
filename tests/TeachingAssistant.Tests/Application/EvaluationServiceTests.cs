using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

public class EvaluationServiceTests
{
    private static DraftComparison Scores(double? ai, double? teacher, double max = 100, params MistakeComparison[] mistakes) =>
        new(ai, teacher, max, mistakes, 0, 0, 0);

    [Fact]
    public void Scores_CountAgreementWithin5Percent_TheAverageGap_AndWhichWayItLeans()
    {
        var scores = EvaluationService.Scores(
        [
            Scores(90, 80),   // +10: AI more lenient
            Scores(80, 80),   // exact
            Scores(78, 80),   // -2: within 5
            Scores(45, 40, max: 50), // +10% of 50
            Scores(null, 70), // not compared
        ]);

        Assert.Equal((4, 2, 1, 2, 1), (scores.Compared, scores.WithinTolerance, scores.Exact, scores.AiHigher, scores.AiLower));
        Assert.Equal(5.5, scores.MeanAbsoluteDifference!.Value, 6);
        Assert.Equal(94.5, scores.Accuracy!.Value, 6);
        Assert.Equal(4.5, scores.MeanDifference!.Value, 6);
        Assert.Equal(5, scores.TolerancePercent);
    }

    [Fact]
    public void Buckets_Are5PointSteps_WithTheOuterOnesOpen()
    {
        var buckets = EvaluationService.Scores([Scores(100, 40), Scores(80, 80), Scores(78, 80), Scores(85, 80), Scores(40, 90)]).Buckets;

        Assert.Equal(10, buckets.Count);
        Assert.Equal((-25, -20, 1), (buckets[0].From, buckets[0].To, buckets[0].Count)); // -50 lands in the lowest step
        Assert.Equal(1, buckets.Single(b => b.From == -5).Count);                          // -2
        Assert.Equal(1, buckets.Single(b => b.From == 0).Count);                           // 0
        Assert.Equal(1, buckets.Single(b => b.From == 5).Count);                           // +5
        Assert.Equal((20, 25, 1), (buckets[^1].From, buckets[^1].To, buckets[^1].Count));  // +60 lands in the highest step
    }

    [Fact]
    public void Mistakes_AreCountedByTheAisOwnCategoryAndSeverity_WithPointChanges()
    {
        var mistakes = EvaluationService.Mistakes(
        [
            Scores(90, 80, 100,
                new MistakeComparison(MistakeSeverity.Minor, MistakeCategory.Naming, MistakeDecision.Edited, 3, 1),
                new MistakeComparison(MistakeSeverity.Major, MistakeCategory.Correctness, MistakeDecision.Rejected, 10, 0)),
            Scores(70, 70, 100,
                new MistakeComparison(MistakeSeverity.Minor, MistakeCategory.Naming, MistakeDecision.Kept, 3, 3),
                new MistakeComparison(MistakeSeverity.Style, MistakeCategory.Readability, MistakeDecision.Kept, 0, 0)),
        ]);

        Assert.Equal((4, 2, 1, 1), (mistakes.Total, mistakes.Kept, mistakes.Edited, mistakes.Rejected));
        Assert.Equal((1, 2.0), (mistakes.PointsChanged, mistakes.MeanPointsChange!.Value));
        Assert.Equal((MistakeCategory.Naming, 1, 1, 0),
            (mistakes.ByCategory[0].Category, mistakes.ByCategory[0].Kept, mistakes.ByCategory[0].Edited, mistakes.ByCategory[0].Rejected));
        Assert.Equal([MistakeSeverity.Major, MistakeSeverity.Minor, MistakeSeverity.Style], mistakes.BySeverity.Select(s => s.Severity));
        Assert.Equal(1, mistakes.BySeverity.Single(s => s.Severity == MistakeSeverity.Major).Rejected);
    }

    [Fact]
    public void Discards_CountPoorGradingFirst_AndKeepEachDraftsContext()
    {
        var at = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
        DiscardedDraft Draft(DiscardReason? reason, string? note = null) =>
            new(Guid.NewGuid(), "c-1", "a-1", "s-1", "Nini", "claude-opus-5-5", 70, 5, 100, 1, 2, 3, reason, note, at, at.AddHours(1), null);

        var discards = EvaluationService.Discards(
            [Draft(DiscardReason.Superseded), Draft(DiscardReason.Superseded), Draft(DiscardReason.PoorGrading, "Ignored the rules."), Draft(null)],
            new Dictionary<(string Course, string Assignment), string> { [("c-1", "a-1")] = "Assignment 3" });

        Assert.Equal((4, 1), (discards.Total, discards.PoorGrading));
        Assert.Equal([DiscardReason.PoorGrading, DiscardReason.Superseded, null], discards.ByReason.Select(r => r.Reason));
        Assert.Equal([1, 2, 1], discards.ByReason.Select(r => r.Count));
        var poor = discards.Rows.Single(r => r.Reason == DiscardReason.PoorGrading);
        Assert.Equal(("Assignment 3", "Nini", 75.0, "Ignored the rules.", 6), (poor.AssignmentTitle, poor.StudentName, poor.AiScore!.Value, poor.Note,
            poor.Major + poor.Minor + poor.Style));
    }

    [Fact]
    public void NoReports_GiveZeroCountsAndNoAverages()
    {
        var scores = EvaluationService.Scores([]);
        var mistakes = EvaluationService.Mistakes([]);

        Assert.Equal((0, 0, (double?)null, (double?)null), (scores.Compared, scores.WithinTolerance, scores.MeanAbsoluteDifference, scores.Accuracy));
        Assert.All(scores.Buckets, b => Assert.Equal(0, b.Count));
        Assert.Equal((0, (double?)null), (mistakes.Total, mistakes.MeanPointsChange));
    }
}
