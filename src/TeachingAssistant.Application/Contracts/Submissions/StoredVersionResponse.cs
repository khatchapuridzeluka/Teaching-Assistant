using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record StoredVersionResponse(
    Guid Id,
    int Number,
    VersionStatus Status,
    string? StatusDetail,
    bool HasCode,
    string? ExtractionDetail,
    DateTimeOffset FetchedAt,
    DateTimeOffset? TurnedInAt,
    IReadOnlyList<StoredFileResponse> Files);
