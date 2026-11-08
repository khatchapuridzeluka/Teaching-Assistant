namespace TeachingAssistant.Domain.Ingestion;

public class VersionContent
{
    private VersionContent()
    {
    }

    public VersionContent(
        ContentOrigin origin,
        ContentKind kind,
        string externalId,
        string name,
        string? mimeType,
        string fingerprint,
        string? @ref,
        long? sizeBytes,
        string? storedName,
        string? llmReadyName,
        ContentStatus status)
    {
        Id = Guid.CreateVersion7();
        Origin = origin;
        Kind = kind;
        ExternalId = externalId;
        Name = name;
        MimeType = mimeType;
        Fingerprint = fingerprint;
        Ref = @ref;
        SizeBytes = sizeBytes;
        StoredName = storedName;
        LlmReadyName = llmReadyName;
        Status = status;
    }

    public Guid Id { get; private set; }
    public Guid VersionId { get; private set; }
    public ContentOrigin Origin { get; private set; }
    public ContentKind Kind { get; private set; }

    public string ExternalId { get; private set; } = null!;

    public string Name { get; private set; } = null!;
    public string? MimeType { get; private set; }

    public string Fingerprint { get; private set; } = null!;

    public string? Ref { get; private set; }

    public long? SizeBytes { get; private set; }
    public string? StoredName { get; private set; }

    public string? LlmReadyName { get; private set; }

    public ContentStatus Status { get; private set; }

    public ContentFingerprint ToFingerprint() => new(Origin, ExternalId, Fingerprint);

    internal void SetExtracted(bool extracted)
    {
        if (extracted)
        {
            Status = ContentStatus.Extracted;
        }
        else if (Status == ContentStatus.Extracted)
        {
            Status = ContentStatus.Stored;
        }
    }
}
