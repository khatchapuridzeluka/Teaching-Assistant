namespace TeachingAssistant.Domain.Ingestion;

public class SubmissionVersion
{
    private readonly List<VersionContent> _contents = [];

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

    public string? ContentHash { get; private set; }

    public string StoragePath { get; private set; } = null!;

    public string? ExtractionDetail { get; private set; }
    public DateTimeOffset? TurnedInAt { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public IReadOnlyList<VersionContent> Contents => _contents;

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
