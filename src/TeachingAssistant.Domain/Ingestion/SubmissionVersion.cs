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

    /// <summary>What could not be extracted into code/ (broken or protected archive, no code found); null when all went well.</summary>
    public string? ExtractionDetail { get; private set; }
    public DateTimeOffset? TurnedInAt { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public IReadOnlyList<VersionContent> Contents => _contents;

    /// <summary>
    /// Records the result of extracting the code into code/. Code is derived from the originals, so this may be
    /// recorded again when extraction is re-run; the originals themselves never change.
    /// </summary>
    /// <param name="contentHash">Hash of the extracted files, or null when no code was extracted.</param>
    /// <param name="extractedStoredNames">Stored names of the archives and source files whose code was extracted.</param>
    /// <param name="detail">What could not be extracted; null when nothing went wrong.</param>
    public void RecordExtraction(string? contentHash, IEnumerable<string> extractedStoredNames, string? detail)
    {
        ContentHash = contentHash;
        ExtractionDetail = detail;

        var extracted = extractedStoredNames.ToHashSet();
        foreach (var content in _contents)
        {
            content.SetExtracted(content.StoredName is not null && extracted.Contains(content.StoredName));
        }
    }
}
