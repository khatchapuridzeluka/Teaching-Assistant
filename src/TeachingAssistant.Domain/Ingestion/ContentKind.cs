namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// What a piece of content is for grading. Archives, source files and repositories hold the code;
/// documents and images are supporting material (reports, output screenshots).
/// </summary>
public enum ContentKind
{
    CodeArchive,
    SourceFile,
    Repository,
    Document,
    Image,
    Other,
}
