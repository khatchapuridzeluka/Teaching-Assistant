namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// Where a piece of submitted content comes from. One submission can mix origins (a GitHub link plus a Drive report).
/// </summary>
public enum ContentOrigin
{
    Drive,
    GitHub,

    /// <summary>A web link that is not a GitHub repository.</summary>
    Link,

    Upload,
}
