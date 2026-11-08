namespace TeachingAssistant.Domain.Ingestion;

public class InstructionsContent
{
    private InstructionsContent()
    {
    }

    public InstructionsContent(
        ContentOrigin origin,
        string externalId,
        string name,
        string? mimeType,
        string fingerprint,
        long? sizeBytes,
        string? storedName,
        ContentStatus status)
    {
        Id = Guid.CreateVersion7();
        Origin = origin;
        ExternalId = externalId;
        Name = name;
        MimeType = mimeType;
        Fingerprint = fingerprint;
        SizeBytes = sizeBytes;
        StoredName = storedName;
        Status = status;
    }

    public Guid Id { get; private set; }
    public Guid VersionId { get; private set; }
    public ContentOrigin Origin { get; private set; }

    public string ExternalId { get; private set; } = null!;

    public string Name { get; private set; } = null!;
    public string? MimeType { get; private set; }

    public string Fingerprint { get; private set; } = null!;

    public long? SizeBytes { get; private set; }
    public string? StoredName { get; private set; }
    public ContentStatus Status { get; private set; }

    public ContentFingerprint ToFingerprint() => new(Origin, ExternalId, Fingerprint);
}
