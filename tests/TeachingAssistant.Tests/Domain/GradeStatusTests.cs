using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Domain;

public class GradeStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(80.0, false, -1, GradeStatus.Graded)]
    [InlineData(null, true, -1, GradeStatus.AwaitingGrade)]
    [InlineData(null, false, -1, GradeStatus.Missing)]
    [InlineData(null, false, 1, GradeStatus.NotDue)]
    public void GradeStatusAt_FollowsGradeThenTurnInThenDueDate(double? grade, bool turnedIn, int dueInDays, GradeStatus expected)
    {
        var result = new AssignmentResult(100, Now.AddDays(dueInDays), grade, turnedIn);

        Assert.Equal(expected, result.GradeStatusAt(Now));
    }

    [Fact]
    public void NoDueDate_AndNothingTurnedIn_IsNotSubmitted_NotMissing()
    {
        var result = new AssignmentResult(100, null, null, false);

        Assert.Equal(GradeStatus.NotSubmitted, result.GradeStatusAt(Now));
    }
}
