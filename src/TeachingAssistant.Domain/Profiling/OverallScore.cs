namespace TeachingAssistant.Domain.Profiling;

/// <summary>One assignment of a course as it counts for a student's overall score.</summary>
/// <param name="MaxPoints">The assignment's maximum; null or 0 for ungraded work, which never counts.</param>
/// <param name="DueAt">The deadline; null when there is none.</param>
/// <param name="FinalGrade">The student's final grade; null when the work has no grade (missing or not graded yet).</param>
/// <param name="TurnedIn">The student handed the work in (turned in or returned).</param>
public sealed record AssignmentResult(double? MaxPoints, DateTimeOffset? DueAt, double? FinalGrade, bool TurnedIn)
{
    /// <summary>Not handed in and past the deadline: counts as 0.</summary>
    public bool IsMissing(DateTimeOffset now) =>
        MaxPoints is > 0 && FinalGrade is null && !TurnedIn && DueAt is { } due && due <= now;
}

/// <summary>
/// A student's overall score in a course: final points earned over the points possible, so a 100-point homework weighs
/// more than a 10-point one. Graded work always counts. Work that was not handed in counts as 0 once its deadline has
/// passed (missing work). Left out: work handed in but not graded yet, and work not due yet, so neither lowers the score.
/// </summary>
public sealed record OverallScore(double Earned, double Possible, int Counted)
{
    /// <summary>Earned as a percentage of possible; null when nothing counts yet.</summary>
    public double? Percent => Possible > 0 ? Earned / Possible * 100 : null;

    public static OverallScore Compute(IEnumerable<AssignmentResult> assignments, DateTimeOffset now)
    {
        double earned = 0, possible = 0;
        var counted = 0;

        foreach (var assignment in assignments)
        {
            if (assignment.MaxPoints is not > 0)
            {
                continue; // ungraded work
            }

            if (assignment.FinalGrade is { } grade)
            {
                earned += grade;
            }
            else if (!assignment.IsMissing(now))
            {
                continue; // waiting for a grade, or not due yet: not missing
            }

            possible += assignment.MaxPoints.Value;
            counted++;
        }

        return new OverallScore(earned, possible, counted);
    }
}
