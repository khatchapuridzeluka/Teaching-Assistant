namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// One snapshot of a submission (v1, v2, …). Immutable once created: a resubmission becomes a new version.
/// StoragePath is relative to the storage root and holds original/ (as downloaded) and code/ (extracted).
/// </summary>
public class SubmissionVersion
{
    private readonly List<VersionContent> _contents = [];

    // For EF Core.
    private SubmissionVersion()
    {
    }

    internal SubmissionVersion(
        Guid submissionId,
        int number,
        VersionStatus status,
        string? statusDetail,
        string? contentHash,
        string storagePath,
        DateTimeOffset? turnedInAt,
        DateTimeOffset fetchedAt,
        IEnumerable<VersionContent> contents)
    {
        Id = Guid.CreateVersion7();
        SubmissionId = submissionId;
        Number = number;
        Status = status;
        StatusDetail = statusDetail;
        ContentHash = contentHash;
        StoragePath = storagePath;
        TurnedInAt = turnedInAt?.ToUniversalTime();
        FetchedAt = fetchedAt.ToUniversalTime();
        _contents.AddRange(contents);
    }

    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public int Number { get; private set; }
    public VersionStatus Status { get; private set; }
    public string? StatusDetail { get; private set; }

    /// <summary>SHA-256 over the extracted code files (paths and contents); null when no code was extracted.</summary>
    public string? ContentHash { get; private set; }

    public string StoragePath { get; private set; } = null!;
    public DateTimeOffset? TurnedInAt { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public IReadOnlyList<VersionContent> Contents => _contents;
}
