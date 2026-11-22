using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record InstructionsFileResponse(
    Guid Id,
    string Name,
    ContentOrigin Origin,
    string? MimeType,
    long? SizeBytes,
    ContentStatus Status,
    bool Stored,
    string? Url);
