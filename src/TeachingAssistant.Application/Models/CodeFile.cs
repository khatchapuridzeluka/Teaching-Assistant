namespace TeachingAssistant.Application.Models;

public record CodeFile
{
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }
}
