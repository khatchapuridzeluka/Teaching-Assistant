namespace TeachingAssistant.Domain.Profiling;

public sealed record AssignmentResult
{
    public double? MaxPoints { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public double? Grade { get; init; }
    public required bool TurnedIn { get; init; }

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
