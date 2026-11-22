using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record InstructionsFileResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ContentOrigin Origin { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public required ContentStatus Status { get; init; }
    public required bool Stored { get; init; }
    public string? Url { get; init; }
}
