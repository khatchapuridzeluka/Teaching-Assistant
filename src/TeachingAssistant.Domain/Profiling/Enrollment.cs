namespace TeachingAssistant.Domain.Profiling;

public class Enrollment
{
    private Enrollment()
    {
    }

    internal Enrollment(Guid studentId, string courseId, DateTimeOffset seenAt)
    {
        Id = Guid.CreateVersion7();
        StudentId = studentId;
        CourseId = courseId;
        Active = true;
        EnrolledAt = seenAt.ToUniversalTime();
        LastSeenAt = EnrolledAt;
    }

    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public string CourseId { get; private set; } = null!;

    public bool Active { get; private set; }

    public DateTimeOffset EnrolledAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    internal void MarkSeen(DateTimeOffset seenAt)
    {
        Active = true;
        LastSeenAt = seenAt.ToUniversalTime();
    }

    internal void Deactivate() => Active = false;
}
