using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record StoredInstructionsResponse(
    Guid VersionId,
    int Number,
    int VersionCount,
    string Title,
    string? Description,
    double? MaxPoints,
    DateTimeOffset FetchedAt,
    IReadOnlyList<InstructionsFileResponse> Files);

public record InstructionsFileResponse(
    Guid Id,
    string Name,
    ContentOrigin Origin,
    string? MimeType,
    long? SizeBytes,
    ContentStatus Status,
    bool Stored,
    string? Url);
