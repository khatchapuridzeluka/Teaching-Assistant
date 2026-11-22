using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Domain;

public class OverallScoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Past = Now.AddDays(-7);
    private static readonly DateTimeOffset Future = Now.AddDays(7);

    [Fact]
    public void GradedWork_IsPointsEarnedOverPointsPossible()
    {
        var score = OverallScore.Compute([new() { MaxPoints = 100, DueAt = Past, Grade = 90, TurnedIn = true }, new()
        {
            MaxPoints = 50,
            DueAt = Past,
            Grade = 25,
            TurnedIn = true,
        }], Now);

        Assert.Equal((115, 150, 2), (score.Earned, score.Possible, score.Counted));
        Assert.Equal(115.0 / 150 * 100, score.Percent);
    }

    [Fact]
    public void MissingWorkPastTheDeadline_CountsAsZero()
    {
        var score = OverallScore.Compute([new() { MaxPoints = 100, DueAt = Past, Grade = 80, TurnedIn = true }, new() { MaxPoints = 100, DueAt = Past, TurnedIn = false }], Now);

        Assert.Equal((80, 200, 2), (score.Earned, score.Possible, score.Counted));
        Assert.Equal(40, score.Percent);
    }

    [Fact]
    public void UngradedWorkNotDueYet_IsLeftOut()
    {
        var score = OverallScore.Compute([new() { MaxPoints = 100, DueAt = Past, Grade = 80, TurnedIn = true }, new() { MaxPoints = 100, DueAt = Future, TurnedIn = false }, new()
        {
            MaxPoints = 100,
            TurnedIn = false,
        }], Now);

        Assert.Equal((80, 100, 1), (score.Earned, score.Possible, score.Counted));
    }

    [Fact]
    public void WorkGradedBeforeTheDeadline_Counts()
    {
        var score = OverallScore.Compute([new() { MaxPoints = 100, DueAt = Future, Grade = 70, TurnedIn = true }], Now);

        Assert.Equal((70, 100, 1), (score.Earned, score.Possible, score.Counted));
    }

    [Fact]
    public void TurnedInButNotGradedYet_IsLeftOut_EvenPastTheDeadline()
    {
        var score = OverallScore.Compute([new() { MaxPoints = 100, DueAt = Past, Grade = 80, TurnedIn = true }, new() { MaxPoints = 100, DueAt = Past, TurnedIn = true }], Now);

        Assert.Equal((80, 100, 1), (score.Earned, score.Possible, score.Counted));
        Assert.False(new AssignmentResult { MaxPoints = 100, DueAt = Past, TurnedIn = true }.IsMissing(Now));
        Assert.True(new AssignmentResult { MaxPoints = 100, DueAt = Past, TurnedIn = false }.IsMissing(Now));
    }

    [Fact]
    public void AssignmentsWithoutPoints_NeverCount()
    {
        var score = OverallScore.Compute([new() { DueAt = Past, TurnedIn = false }, new() { MaxPoints = 0, DueAt = Past, TurnedIn = false }], Now);

        Assert.Equal(0, score.Counted);
        Assert.Null(score.Percent);
    }
}
