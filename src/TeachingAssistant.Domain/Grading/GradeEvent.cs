namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// One change in a submission's grade as the source (Classroom) recorded it: a copy of its grade history, which is
/// where the initial and final scores come from. Never created by us: grades are given in Classroom only.
/// </summary>
public class GradeEvent
{
    // For EF Core.
    private GradeEvent()
    {
    }

    public GradeEvent(GradeEventKind kind, double? pointsEarned, double? maxPoints, DateTimeOffset gradedAt)
    {
        Id = Guid.CreateVersion7();
        Kind = kind;
        PointsEarned = pointsEarned;
        MaxPoints = maxPoints;
        GradedAt = gradedAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public GradeEventKind Kind { get; private set; }

    /// <summary>The grade after the change; null when the grade was removed.</summary>
    public double? PointsEarned { get; private set; }

    public double? MaxPoints { get; private set; }
    public DateTimeOffset GradedAt { get; private set; }

    internal void AttachTo(Guid submissionId) => SubmissionId = submissionId;

    /// <summary>The same change as another one (ignoring IDs): used to skip rewriting an unchanged history.</summary>
    public bool SameAs(GradeEvent other) =>
        Kind == other.Kind && PointsEarned == other.PointsEarned && MaxPoints == other.MaxPoints && GradedAt == other.GradedAt;
}
