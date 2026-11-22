namespace TeachingAssistant.Application.Models;

public record StoredFileContent
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}
