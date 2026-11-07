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
        var score = OverallScore.Compute([new(100, Past, 90, true), new(50, Past, 25, true)], Now);

        Assert.Equal((115, 150, 2), (score.Earned, score.Possible, score.Counted));
        Assert.Equal(115.0 / 150 * 100, score.Percent);
    }

    [Fact]
    public void MissingWorkPastTheDeadline_CountsAsZero()
    {
        var score = OverallScore.Compute([new(100, Past, 80, true), new(100, Past, null, false)], Now);

        Assert.Equal((80, 200, 2), (score.Earned, score.Possible, score.Counted));
        Assert.Equal(40, score.Percent);
    }

    [Fact]
    public void UngradedWorkNotDueYet_IsLeftOut()
    {
        var score = OverallScore.Compute([new(100, Past, 80, true), new(100, Future, null, false), new(100, null, null, false)], Now);

        Assert.Equal((80, 100, 1), (score.Earned, score.Possible, score.Counted));
    }

    [Fact]
    public void WorkGradedBeforeTheDeadline_Counts()
    {
        var score = OverallScore.Compute([new(100, Future, 70, true)], Now);

        Assert.Equal((70, 100, 1), (score.Earned, score.Possible, score.Counted));
    }

    [Fact]
    public void TurnedInButNotGradedYet_IsLeftOut_EvenPastTheDeadline()
    {
        var score = OverallScore.Compute([new(100, Past, 80, true), new(100, Past, null, true)], Now);

        Assert.Equal((80, 100, 1), (score.Earned, score.Possible, score.Counted));
        Assert.False(new AssignmentResult(100, Past, null, true).IsMissing(Now));
        Assert.True(new AssignmentResult(100, Past, null, false).IsMissing(Now));
    }

    [Fact]
    public void AssignmentsWithoutPoints_NeverCount()
    {
        var score = OverallScore.Compute([new(null, Past, null, false), new(0, Past, null, false)], Now);

        Assert.Equal(0, score.Counted);
        Assert.Null(score.Percent);
    }
}
