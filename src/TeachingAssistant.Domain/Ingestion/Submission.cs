using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// One student's work on one assignment, across all its versions. Course, assignment and student are the
/// source's own IDs: the source (Classroom) stays the owner of that data, we only point to it.
/// A submission exists for every student of an assignment, also before any files were fetched (no versions yet), so
/// missing work and grades are known for the whole class. Grades are a copy of the source's: given in Classroom only.
/// </summary>
public class Submission
{
    private readonly List<SubmissionVersion> _versions = [];
    private readonly List<GradeEvent> _gradeEvents = [];

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
        string storageFolder,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Source = source;
        CourseId = courseId;
        AssignmentId = assignmentId;
        StudentId = studentId;
        StudentName = studentName;
        ExternalSubmissionId = externalSubmissionId;
        StorageFolder = storageFolder;
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

    /// <summary>
    /// Folder holding all versions' files, relative to the storage root. Chosen once when the submission is first
    /// stored, so later renames of the course or student never move existing files.
    /// </summary>
    public string StorageFolder { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<SubmissionVersion> Versions => _versions;

    public SubmissionVersion? LatestVersion => _versions.MaxBy(v => v.Number);

    public int NextVersionNumber => (LatestVersion?.Number ?? 0) + 1;

    /// <summary>The source's state as of the last sync, e.g. CREATED, TURNED_IN, RETURNED; null before the first one.</summary>
    public string? State { get; private set; }

    public bool Late { get; private set; }

    /// <summary>Handed in: turned in, or turned in and returned by the teacher (Classroom's states).</summary>
    public bool IsTurnedIn => State is "TURNED_IN" or "RETURNED";

    /// <summary>The grade the student sees in the source now; null when there is none.</summary>
    public double? AssignedGrade { get; private set; }

    /// <summary>When state and grades were last copied from the source.</summary>
    public DateTimeOffset? StatusSyncedAt { get; private set; }

    /// <summary>The source's grade history, oldest first.</summary>
    public IReadOnlyList<GradeEvent> GradeEvents => _gradeEvents;

    /// <summary>The first grade the student saw (see <see cref="Grades"/>).</summary>
    public double? InitialGrade => Grades.Initial(_gradeEvents);

    /// <summary>The latest grade the student saw (see <see cref="Grades"/>).</summary>
    public double? FinalGrade => Grades.Final(_gradeEvents, AssignedGrade);

    public void UpdateStudentName(string studentName) => StudentName = studentName;

    /// <summary>
    /// Copies the source's state, late flag, current grade and grade history. The history is replaced only when it
    /// differs, so an unchanged sync writes nothing. Requires the grade events to be loaded.
    /// </summary>
    public void RecordStatus(string state, bool late, double? assignedGrade, IEnumerable<GradeEvent> history, DateTimeOffset syncedAt)
    {
        State = state;
        Late = late;
        AssignedGrade = assignedGrade;
        StatusSyncedAt = syncedAt.ToUniversalTime();

        var ordered = history.OrderBy(e => e.GradedAt).ToList();
        if (ordered.Count == _gradeEvents.Count && ordered.Zip(_gradeEvents).All(pair => pair.First.SameAs(pair.Second)))
        {
            return;
        }

        _gradeEvents.Clear();
        foreach (var gradeEvent in ordered)
        {
            gradeEvent.AttachTo(Id);
            _gradeEvents.Add(gradeEvent);
        }
    }

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
