namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// The teacher's instructions for one assignment across all their versions: the task statement and the files attached in
/// the source (usually a Word or PDF document with the tasks and points). Stored when submissions are fetched, so grading
/// works from a saved copy and every grade can point to the exact instructions it used. Mirrors <see cref="Submission"/>.
/// </summary>
public class AssignmentInstructions
{
    private readonly List<InstructionsVersion> _versions = [];

    // For EF Core.
    private AssignmentInstructions()
    {
    }

    public AssignmentInstructions(
        SubmissionSource source, string courseId, string assignmentId, string storageFolder, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Source = source;
        CourseId = courseId;
        AssignmentId = assignmentId;
        StorageFolder = storageFolder;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public SubmissionSource Source { get; private set; }
    public string CourseId { get; private set; } = null!;
    public string AssignmentId { get; private set; } = null!;

    /// <summary>Folder holding all versions' files, relative to the storage root; chosen once, like a submission's.</summary>
    public string StorageFolder { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<InstructionsVersion> Versions => _versions;

    public InstructionsVersion? LatestVersion => _versions.MaxBy(v => v.Number);

    public int NextVersionNumber => (LatestVersion?.Number ?? 0) + 1;

    /// <summary>
    /// True when what the source reports now differs from the latest version, or when there is no version yet: another
    /// title, task statement or maximum points, or an attached file replaced, added or removed. Order does not matter.
    /// </summary>
    public bool HasChanged(string title, string? description, double? maxPoints, IEnumerable<ContentFingerprint> materials) =>
        LatestVersion is not { } latest
        || latest.Title != title
        || latest.Description != description
        || latest.MaxPoints != maxPoints
        || !latest.Contents.Select(c => c.ToFingerprint()).ToHashSet().SetEquals(materials);

    /// <summary>
    /// Records a new snapshot as the next version. Callers write the files to storagePath first,
    /// so a version only exists once its files do.
    /// </summary>
    public InstructionsVersion AddVersion(
        string title,
        string? description,
        double? maxPoints,
        string storagePath,
        DateTimeOffset fetchedAt,
        IEnumerable<InstructionsContent> contents)
    {
        var version = new InstructionsVersion(
            Id, NextVersionNumber, title, description, maxPoints, storagePath, fetchedAt, contents);
        _versions.Add(version);
        return version;
    }
}
