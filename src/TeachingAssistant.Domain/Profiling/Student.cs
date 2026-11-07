using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Domain.Profiling;

/// <summary>
/// A student as the source (Classroom) knows them, kept locally so profiles and scores work without asking the source.
/// One per person, identified by the source's user ID; the courses they are in are their enrollments.
/// </summary>
public class Student
{
    private readonly List<Enrollment> _enrollments = [];

    // For EF Core.
    private Student()
    {
    }

    public Student(SubmissionSource source, string externalUserId, string name, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Source = source;
        ExternalUserId = externalUserId;
        Name = name;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public SubmissionSource Source { get; private set; }
    public string ExternalUserId { get; private set; } = null!;

    /// <summary>Name as of the last roster sync; the source owns it.</summary>
    public string Name { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<Enrollment> Enrollments => _enrollments;

    public void Rename(string name) => Name = name;

    /// <summary>Records that the student is on the course's roster now; enrolls them when they were not.</summary>
    public void EnrollIn(string courseId, DateTimeOffset seenAt)
    {
        var enrollment = _enrollments.FirstOrDefault(e => e.CourseId == courseId);
        if (enrollment is null)
        {
            _enrollments.Add(new Enrollment(Id, courseId, seenAt));
            return;
        }

        enrollment.MarkSeen(seenAt);
    }

    /// <summary>
    /// The student is no longer on the course's roster. The enrollment is kept, inactive, so their past work and scores
    /// stay readable.
    /// </summary>
    public void LeaveCourse(string courseId) =>
        _enrollments.FirstOrDefault(e => e.CourseId == courseId)?.Deactivate();

    public bool IsEnrolledIn(string courseId) => _enrollments.Any(e => e.CourseId == courseId && e.Active);
}
