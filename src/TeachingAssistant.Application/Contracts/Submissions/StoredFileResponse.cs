using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record StoredFileResponse(
    Guid Id,
    string Name,
    ContentKind Kind,
    string? MimeType,
    long? SizeBytes,
    ContentStatus Status,
    bool Stored);
