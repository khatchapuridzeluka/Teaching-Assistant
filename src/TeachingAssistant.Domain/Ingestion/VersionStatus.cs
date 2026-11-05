namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// Overall result of fetching one version.
/// </summary>
public enum VersionStatus
{
    /// <summary>All content stored.</summary>
    Ready,

    /// <summary>Some content is unsupported or too large; the rest is usable. StatusDetail says what.</summary>
    PartiallySupported,

    /// <summary>Nothing usable could be stored. StatusDetail says why.</summary>
    Failed,
}
