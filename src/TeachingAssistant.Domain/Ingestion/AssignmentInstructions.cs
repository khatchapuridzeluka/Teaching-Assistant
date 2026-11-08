namespace TeachingAssistant.Domain.Ingestion;

public class AssignmentInstructions
{
    private readonly List<InstructionsVersion> _versions = [];

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

    public string StorageFolder { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<InstructionsVersion> Versions => _versions;

    public InstructionsVersion? LatestVersion => _versions.MaxBy(v => v.Number);

    public int NextVersionNumber => (LatestVersion?.Number ?? 0) + 1;

    public bool HasChanged(string title, string? description, double? maxPoints, IEnumerable<ContentFingerprint> materials) =>
        LatestVersion is not { } latest
        || latest.Title != title
        || latest.Description != description
        || latest.MaxPoints != maxPoints
        || !latest.Contents.Select(c => c.ToFingerprint()).ToHashSet().SetEquals(materials);

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
