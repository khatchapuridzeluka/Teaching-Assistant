namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// One student's work on one assignment, across all its versions. Course, assignment and student are the
/// source's own IDs: the source (Classroom) stays the owner of that data, we only point to it.
/// </summary>
public class Submission
{
    private readonly List<SubmissionVersion> _versions = [];

    // For EF Core.
    private Submission()
    {
    }

    public Submission(
        SubmissionSource source,
        string courseId,
        string assignmentId,
        string studentId,
        string studentName,
        string externalSubmissionId,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Source = source;
        CourseId = courseId;
        AssignmentId = assignmentId;
        StudentId = studentId;
        StudentName = studentName;
        ExternalSubmissionId = externalSubmissionId;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public SubmissionSource Source { get; private set; }
    public string CourseId { get; private set; } = null!;
    public string AssignmentId { get; private set; } = null!;
    public string StudentId { get; private set; } = null!;

    /// <summary>Name as of the last fetch; for display only.</summary>
    public string StudentName { get; private set; } = null!;

    public string ExternalSubmissionId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<SubmissionVersion> Versions => _versions;

    public SubmissionVersion? LatestVersion => _versions.MaxBy(v => v.Number);

    public int NextVersionNumber => (LatestVersion?.Number ?? 0) + 1;

    public void UpdateStudentName(string studentName) => StudentName = studentName;

    /// <summary>
    /// True when the content the source reports now differs from the latest version's content,
    /// or when there is no version yet. Order does not matter; a replaced, added or removed file counts as a change.
    /// </summary>
    public bool HasChanged(IEnumerable<ContentFingerprint> current) =>
        LatestVersion is not { } latest
        || !latest.Contents.Select(c => c.ToFingerprint()).ToHashSet().SetEquals(current);

    /// <summary>
    /// Records a new snapshot as the next version. Callers write the files to storagePath first,
    /// so a version only exists once its files do.
    /// </summary>
    public SubmissionVersion AddVersion(
        VersionStatus status,
        string? statusDetail,
        string? contentHash,
        string storagePath,
        DateTimeOffset? turnedInAt,
        DateTimeOffset fetchedAt,
        IEnumerable<VersionContent> contents)
    {
        var version = new SubmissionVersion(
            Id, NextVersionNumber, status, statusDetail, contentHash, storagePath, turnedInAt, fetchedAt, contents);
        _versions.Add(version);
        return version;
    }
}
