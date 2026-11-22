using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Models;

public record ExtractableFile
{
    public required string StoredName { get; init; }
    public required ContentKind Kind { get; init; }
}

public record CodeExtraction
{
    public string? ContentHash { get; init; }
    public required int FileCount { get; init; }
    public required IReadOnlyList<string> ExtractedStoredNames { get; init; }
    public required IReadOnlyList<string> Problems { get; init; }
}

public record CodeFile
{
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }
}
