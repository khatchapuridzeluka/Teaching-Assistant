using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Domain.Profiling;

public class Student
{
    private readonly List<Enrollment> _enrollments = [];

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

    public string Name { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<Enrollment> Enrollments => _enrollments;

    public static readonly TimeSpan PhotoRecheckAfter = TimeSpan.FromDays(30);

    public string? PhotoUrl { get; private set; }

    public string? PhotoHash { get; private set; }

    public string? PhotoStoredName { get; private set; }

    public string? PhotoContentType { get; private set; }
    public DateTimeOffset? PhotoFetchedAt { get; private set; }

    public void Rename(string name) => Name = name;

    public bool NeedsPhoto(string? photoUrl, DateTimeOffset now) =>
        photoUrl is not null
        && (PhotoStoredName is null || photoUrl != PhotoUrl || PhotoFetchedAt is not { } fetched || now - fetched > PhotoRecheckAfter);

    public bool RecordPhoto(string photoUrl, string hash, string storedName, string contentType, DateTimeOffset fetchedAt)
    {
        var changed = hash != PhotoHash || PhotoStoredName is null;
        PhotoUrl = photoUrl;
        PhotoHash = hash;
        PhotoStoredName = storedName;
        PhotoContentType = contentType;
        PhotoFetchedAt = fetchedAt.ToUniversalTime();
        return changed;
    }

    public void RemovePhoto()
    {
        PhotoUrl = null;
        PhotoHash = null;
        PhotoStoredName = null;
        PhotoContentType = null;
        PhotoFetchedAt = null;
    }

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

    public void LeaveCourse(string courseId) =>
        _enrollments.FirstOrDefault(e => e.CourseId == courseId)?.Deactivate();

    public bool IsEnrolledIn(string courseId) => _enrollments.Any(e => e.CourseId == courseId && e.Active);
}
