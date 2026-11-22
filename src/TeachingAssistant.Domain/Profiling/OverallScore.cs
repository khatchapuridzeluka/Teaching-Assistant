namespace TeachingAssistant.Domain.Profiling;

public sealed record OverallScore
{
    public required double Earned { get; init; }
    public required double Possible { get; init; }
    public required int Counted { get; init; }

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

        return new OverallScore { Earned = earned, Possible = possible, Counted = counted };
    }
}
