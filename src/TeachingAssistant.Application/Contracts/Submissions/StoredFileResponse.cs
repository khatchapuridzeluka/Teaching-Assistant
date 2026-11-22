using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record StoredFileResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ContentKind Kind { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public required ContentStatus Status { get; init; }
    public required bool Stored { get; init; }
}
