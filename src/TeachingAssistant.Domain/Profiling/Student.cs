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

    /// <summary>How long a stored photo is trusted before it is downloaded again to catch a change behind the same URL.</summary>
    public static readonly TimeSpan PhotoRecheckAfter = TimeSpan.FromDays(30);

    /// <summary>The source's photo URL as last seen; a different URL means a new photo. Null when there is none.</summary>
    public string? PhotoUrl { get; private set; }

    /// <summary>SHA-256 of the stored photo: tells a real change from a new URL for the same image, and is the browser's ETag.</summary>
    public string? PhotoHash { get; private set; }

    /// <summary>Our stored copy (one per student, replaced in place; no history). Null when there is none.</summary>
    public string? PhotoStoredName { get; private set; }

    public string? PhotoContentType { get; private set; }
    public DateTimeOffset? PhotoFetchedAt { get; private set; }

    public void Rename(string name) => Name = name;

    /// <summary>
    /// Whether the photo the source reports now must be downloaded: when there is one and we have no copy, its URL changed,
    /// or our copy is older than <see cref="PhotoRecheckAfter"/> (a changed image behind an unchanged URL is rare but possible).
    /// </summary>
    public bool NeedsPhoto(string? photoUrl, DateTimeOffset now) =>
        photoUrl is not null
        && (PhotoStoredName is null || photoUrl != PhotoUrl || PhotoFetchedAt is not { } fetched || now - fetched > PhotoRecheckAfter);

    /// <summary>
    /// Records a downloaded photo. Returns true when the stored file must be (re)written: the image itself is new; false when
    /// the bytes are the same as our copy (only the URL or the check time moved).
    /// </summary>
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

    /// <summary>The student has no photo in the source any more: forget ours (the caller deletes the file).</summary>
    public void RemovePhoto()
    {
        PhotoUrl = null;
        PhotoHash = null;
        PhotoStoredName = null;
        PhotoContentType = null;
        PhotoFetchedAt = null;
    }

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
