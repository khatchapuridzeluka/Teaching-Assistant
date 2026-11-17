using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Ingestion;

public class Submission
{
    private readonly List<SubmissionVersion> _versions = [];
    private readonly List<GradeEvent> _gradeEvents = [];

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

    public string StudentName { get; private set; } = null!;

    public string ExternalSubmissionId { get; private set; } = null!;

    public string StorageFolder { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<SubmissionVersion> Versions => _versions;

    public SubmissionVersion? LatestVersion => _versions.MaxBy(v => v.Number);

    public int NextVersionNumber => (LatestVersion?.Number ?? 0) + 1;

    public string? State { get; private set; }

    public bool Late { get; private set; }

    public bool IsTurnedIn => State is "TURNED_IN" or "RETURNED";

    public double? AssignedGrade { get; private set; }

    public DateTimeOffset? StatusSyncedAt { get; private set; }

    public IReadOnlyList<GradeEvent> GradeEvents => _gradeEvents;

    /// <summary>The latest grade returned in Classroom; null when none was returned.</summary>
    public double? Grade => Grades.Latest(_gradeEvents, AssignedGrade);

    public void UpdateStudentName(string studentName) => StudentName = studentName;

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

    public bool HasChanged(IEnumerable<ContentFingerprint> current) =>
        LatestVersion is not { } latest
        || !latest.Contents.Select(c => c.ToFingerprint()).ToHashSet().SetEquals(current);

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
