namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// One material attached to an assignment, in one instructions version: a Drive file or a link. Name is what the teacher
/// called it; StoredName is the file name under the version's original/ folder, or null when nothing was stored (a link,
/// a video, or a format the grader cannot read). Mirrors <see cref="VersionContent"/>.
/// </summary>
public class InstructionsContent
{
    // For EF Core.
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

    /// <summary>Drive file ID, or the link itself.</summary>
    public string ExternalId { get; private set; } = null!;

    public string Name { get; private set; } = null!;
    public string? MimeType { get; private set; }

    /// <summary>Drive md5Checksum or Google Docs version; changes when the teacher replaces or edits the file.</summary>
    public string Fingerprint { get; private set; } = null!;

    public long? SizeBytes { get; private set; }
    public string? StoredName { get; private set; }
    public ContentStatus Status { get; private set; }

    public ContentFingerprint ToFingerprint() => new(Origin, ExternalId, Fingerprint);
}
