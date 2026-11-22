using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

public class EvaluationServiceTests
{
    private static DraftComparison Scores(double? ai, double? teacher, double max = 100, params MistakeComparison[] mistakes) =>
        new()
        {
            AiTotal = ai,
            TeacherTotal = teacher,
            MaxScore = max,
            Mistakes = mistakes,
            StrengthsKept = 0,
            StrengthsEdited = 0,
            StrengthsRejected = 0,
        };

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
                }),
            Scores(70, 70, 100,
                new MistakeComparison
                {
                    Severity = MistakeSeverity.Minor,
                    Category = MistakeCategory.Naming,
                    Decision = MistakeDecision.Kept,
                    AiPoints = 3,
                    TeacherPoints = 3,
                },
                new MistakeComparison
                {
                    Severity = MistakeSeverity.Style,
                    Category = MistakeCategory.Readability,
                    Decision = MistakeDecision.Kept,
                    AiPoints = 0,
                    TeacherPoints = 0,
                }),
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
            new()
            {
                GradingId = Guid.NewGuid(),
                CourseId = "c-1",
                AssignmentId = "a-1",
                StudentId = "s-1",
                StudentName = "Nini",
                Model = "claude-opus-5-5",
                Score = 70,
                Bonus = 5,
                MaxScore = 100,
                Major = 1,
                Minor = 2,
                Style = 3,
                Reason = reason,
                Note = note,
                CreatedAt = at,
                DiscardedAt = at.AddHours(1),
            };

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
    public void FoundForAModel_CountsYourMistakesFound_PerCategoryAndSeverity()
    {
        var first = new RunComparison
        {
            ModelTotal = 80,
            TeacherTotal = 80,
            MaxScore = 100,
            TeacherMistakes = 2,
            Found = 1,
            RepeatedFalseAlarms = 1,
            Unreviewed = 2,
            TeacherResults =
            [
                new TeacherMistakeResult { Category = MistakeCategory.Naming, Severity = MistakeSeverity.Minor, Found = true },
                new TeacherMistakeResult { Category = MistakeCategory.Design, Severity = MistakeSeverity.Major, Found = false },
            ],
        };
        var second = new RunComparison
        {
            ModelTotal = 70,
            TeacherTotal = 75,
            MaxScore = 100,
            TeacherMistakes = 1,
            Found = 1,
            RepeatedFalseAlarms = 0,
            Unreviewed = 1,
            TeacherResults = [new TeacherMistakeResult
            {
                Category = MistakeCategory.Naming,
                Severity = MistakeSeverity.Minor,
                Found = true,
            }],
        };

        var found = EvaluationService.Found(5, [first, second]);

        Assert.Equal((5, 3, 2, 1, 3), (found.ApprovedReports, found.TeacherMistakes, found.Found, found.RepeatedFalseAlarms, found.Unreviewed));
        Assert.Equal([(MistakeCategory.Naming, 2, 2), (MistakeCategory.Design, 1, 0)], found.ByCategory.Select(c => (c.Category, c.TeacherMistakes, c.Found)));
        Assert.Equal([(MistakeSeverity.Major, 1, 0), (MistakeSeverity.Minor, 2, 2)], found.BySeverity.Select(s => (s.Severity, s.TeacherMistakes, s.Found)));
    }

    [Fact]
    public void AModelRow_AddsUpAccuracy_MistakesFound_AndTime()
    {
        EvaluationRunResult Result(int seconds)
        {
            var result = new EvaluationRunResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "claude-sonnet-5-5", DateTimeOffset.UtcNow);
            result.Complete("claude-sonnet-5-5", 0, 100, null, [], BuildStatus.Compiled, TimeSpan.FromSeconds(seconds), DateTimeOffset.UtcNow);
            return result;
        }

        var row = EvaluationRunService.Row("claude-sonnet-5-5",
        [
            (Result(30), new RunComparison
            {
                ModelTotal = 82,
                TeacherTotal = 80,
                MaxScore = 100,
                TeacherMistakes = 4,
                Found = 3,
                RepeatedFalseAlarms = 1,
                Unreviewed = 0,
            }),
            (Result(50), new RunComparison
            {
                ModelTotal = 70,
                TeacherTotal = 76,
                MaxScore = 100,
                TeacherMistakes = 2,
                Found = 2,
                RepeatedFalseAlarms = 0,
                Unreviewed = 1,
            }),
        ], failed: 1);

        Assert.Equal((2, 2, 1, 1), (row.Graded, row.ScoresCompared, row.WithinTolerance, row.Failed));
        Assert.Equal(96.0, row.Accuracy!.Value, 6);
        Assert.Equal(-2.0, row.MeanDifference!.Value, 6);
        Assert.Equal((6, 5, 1, 1), (row.TeacherMistakes, row.Found, row.RepeatedFalseAlarms, row.Unreviewed));
        Assert.Equal(83.333, row.FoundPercent!.Value, 3);
        Assert.Equal(40.0, row.AverageSeconds!.Value, 6);
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
