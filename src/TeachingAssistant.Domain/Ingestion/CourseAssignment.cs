namespace TeachingAssistant.Domain.Ingestion;

public class CourseAssignment
{
    private CourseAssignment()
    {
    }

    public CourseAssignment(
        SubmissionSource source, string courseId, string externalAssignmentId, string title, double? maxPoints,
        DateTimeOffset? dueAt, DateTimeOffset syncedAt)
    {
        Id = Guid.CreateVersion7();
        Source = source;
        CourseId = courseId;
        ExternalAssignmentId = externalAssignmentId;
        Update(title, maxPoints, dueAt, syncedAt);
    }

    public Guid Id { get; private set; }
    public SubmissionSource Source { get; private set; }
    public string CourseId { get; private set; } = null!;
    public string ExternalAssignmentId { get; private set; } = null!;
    public string Title { get; private set; } = null!;

    public double? MaxPoints { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset SyncedAt { get; private set; }

    public void Update(string title, double? maxPoints, DateTimeOffset? dueAt, DateTimeOffset syncedAt)
    {
        Title = title;
        MaxPoints = maxPoints;
        DueAt = dueAt?.ToUniversalTime();
        SyncedAt = syncedAt.ToUniversalTime();
    }
}
