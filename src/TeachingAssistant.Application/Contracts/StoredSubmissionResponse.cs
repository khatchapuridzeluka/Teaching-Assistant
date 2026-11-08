using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record StoredSubmissionResponse(
    Guid Id,
    string ExternalSubmissionId,
    string StudentName,
    IReadOnlyList<StoredVersionResponse> Versions);

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

public record CodeFileResponse(string Path, long SizeBytes);

public record ExtractionSummary(int Versions, int VersionsWithCode, IReadOnlyList<FetchProblem> Problems);

public record StoredFileResponse(
    Guid Id,
    string Name,
    ContentKind Kind,
    string? MimeType,
    long? SizeBytes,
    ContentStatus Status,
    bool Stored);
