namespace TeachingAssistant.Application.Contracts;

public record CodeFileResponse
{
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }
}
