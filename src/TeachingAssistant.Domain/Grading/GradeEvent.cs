namespace TeachingAssistant.Domain.Grading;

public class GradeEvent
{
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

    public double? PointsEarned { get; private set; }

    public double? MaxPoints { get; private set; }
    public DateTimeOffset GradedAt { get; private set; }

    internal void AttachTo(Guid submissionId) => SubmissionId = submissionId;

    public bool SameAs(GradeEvent other) =>
        Kind == other.Kind && PointsEarned == other.PointsEarned && MaxPoints == other.MaxPoints && GradedAt == other.GradedAt;
}
