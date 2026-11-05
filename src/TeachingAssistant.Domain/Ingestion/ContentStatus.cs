namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// What happened to a piece of content during fetch.
/// </summary>
public enum ContentStatus
{
    /// <summary>Original stored; nothing to extract (document, image).</summary>
    Stored,

    /// <summary>Original stored and its code extracted.</summary>
    Extracted,

    /// <summary>Format we cannot read; original kept and flagged for the teacher.</summary>
    Unsupported,

    /// <summary>Over the size limits; not extracted.</summary>
    TooLarge,
}
