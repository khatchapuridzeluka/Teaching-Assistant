namespace TeachingAssistant.Application.Models;

public record StoredFileLocation
{
    public required string VersionPath { get; init; }
    public required string StoredName { get; init; }
    public required string Name { get; init; }
    public string? MimeType { get; init; }
}

public record StoredFileContent
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}
