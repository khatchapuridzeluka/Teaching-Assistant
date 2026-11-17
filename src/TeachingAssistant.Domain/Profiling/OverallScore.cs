namespace TeachingAssistant.Domain.Profiling;

public sealed record AssignmentResult(double? MaxPoints, DateTimeOffset? DueAt, double? Grade, bool TurnedIn)
{
    public bool IsMissing(DateTimeOffset now) =>
        MaxPoints is > 0 && Grade is null && !TurnedIn && DueAt is { } due && due <= now;

    public GradeStatus GradeStatusAt(DateTimeOffset now)
    {
        if (Grade is not null)
        {
            return GradeStatus.Graded;
        }

        if (TurnedIn)
        {
            return GradeStatus.AwaitingGrade;
        }

        if (IsMissing(now))
        {
            return GradeStatus.Missing;
        }

        if (DueAt is { } due && due > now)
        {
            return GradeStatus.NotDue;
        }

        return GradeStatus.NotSubmitted;
    }
}

public sealed record OverallScore(double Earned, double Possible, int Counted)
{
    public double? Percent => Possible > 0 ? Earned / Possible * 100 : null;

    public static OverallScore Compute(IEnumerable<AssignmentResult> assignments, DateTimeOffset now)
    {
        double earned = 0, possible = 0;
        var counted = 0;

        foreach (var assignment in assignments)
        {
            if (assignment.MaxPoints is not > 0)
            {
                continue;
            }

            if (assignment.Grade is { } grade)
            {
                earned += grade;
            }
            else if (!assignment.IsMissing(now))
            {
                continue;
            }

            possible += assignment.MaxPoints.Value;
            counted++;
        }

        return new OverallScore(earned, possible, counted);
    }
}
