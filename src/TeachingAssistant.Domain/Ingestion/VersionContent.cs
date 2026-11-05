namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// One piece of content in a version: a Drive file, a repository at a commit, or an uploaded file.
/// Name is what the student called it; StoredName is the sanitized file name under the version's original/ folder,
/// or null when nothing was stored (unsupported or too large).
/// </summary>
public class VersionContent
{
    // For EF Core.
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

    /// <summary>Drive file ID or repository URL.</summary>
    public string ExternalId { get; private set; } = null!;

    public string Name { get; private set; } = null!;
    public string? MimeType { get; private set; }

    /// <summary>Drive md5Checksum or commit SHA; changes when the student replaces the content.</summary>
    public string Fingerprint { get; private set; } = null!;

    /// <summary>Branch for repositories; null for files.</summary>
    public string? Ref { get; private set; }

    public long? SizeBytes { get; private set; }
    public string? StoredName { get; private set; }

    /// <summary>File prepared for the language model (e.g. text extracted from a .docx); null when not needed.</summary>
    public string? LlmReadyName { get; private set; }

    public ContentStatus Status { get; private set; }

    public ContentFingerprint ToFingerprint() => new(Origin, ExternalId, Fingerprint);
}
