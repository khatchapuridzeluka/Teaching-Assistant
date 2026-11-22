namespace TeachingAssistant.Application.Contracts;

public record ExportedFile
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}
