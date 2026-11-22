namespace TeachingAssistant.Application.Models;

public record StoredFileLocation
{
    public required string VersionPath { get; init; }
    public required string StoredName { get; init; }
    public required string Name { get; init; }
    public string? MimeType { get; init; }
}
