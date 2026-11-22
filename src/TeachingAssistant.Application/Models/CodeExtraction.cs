namespace TeachingAssistant.Application.Models;

public record CodeExtraction
{
    public string? ContentHash { get; init; }
    public required int FileCount { get; init; }
    public required IReadOnlyList<string> ExtractedStoredNames { get; init; }
    public required IReadOnlyList<string> Problems { get; init; }
}
