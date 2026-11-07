namespace TeachingAssistant.Domain.Profiling;

/// <summary>A student in one course. Inactive once they left the roster; their work stays.</summary>
public class Enrollment
{
    // For EF Core.
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

    /// <summary>On the course's roster as of the last sync.</summary>
    public bool Active { get; private set; }

    public DateTimeOffset EnrolledAt { get; private set; }

    /// <summary>The last sync that found the student on the roster.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    internal void MarkSeen(DateTimeOffset seenAt)
    {
        Active = true;
        LastSeenAt = seenAt.ToUniversalTime();
    }

    internal void Deactivate() => Active = false;
}
