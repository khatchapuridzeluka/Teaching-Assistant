using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Models;

public record ExtractableFile(string StoredName, ContentKind Kind);

public record CodeExtraction(
    string? ContentHash,
    int FileCount,
    IReadOnlyList<string> ExtractedStoredNames,
    IReadOnlyList<string> Problems);

public record CodeFile(string Path, long SizeBytes);
