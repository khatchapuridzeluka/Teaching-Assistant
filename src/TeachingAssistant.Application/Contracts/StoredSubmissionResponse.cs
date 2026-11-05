using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// A submission as stored by fetch, with every version and its files. ExternalSubmissionId is the source's
/// submission ID, matching SubmissionResponse.Id from the live list.
/// </summary>
public record StoredSubmissionResponse(
    Guid Id,
    string ExternalSubmissionId,
    string StudentName,
    IReadOnlyList<StoredVersionResponse> Versions);

/// <summary>
/// One stored version (v1, v2, …). StatusDetail explains what could not be stored; HasCode is true when code was
/// extracted into the version's code folder (see GET /versions/{id}/code); ExtractionDetail explains what could not be extracted.
/// </summary>
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

/// <summary>
/// One extracted file in a version's code folder. Path uses forward slashes and is passed back to read the file.
/// </summary>
public record CodeFileResponse(string Path, long SizeBytes);

/// <summary>
/// What re-extracting an assignment's stored versions did. Problems lists archives that could not be extracted.
/// </summary>
public record ExtractionSummary(int Versions, int VersionsWithCode, IReadOnlyList<FetchProblem> Problems);

/// <summary>
/// One piece of content in a version. Name is what the student called it. Stored is false when nothing was saved
/// (unsupported or too large); otherwise the content is served by GET /stored-files/{id}.
/// </summary>
public record StoredFileResponse(
    Guid Id,
    string Name,
    ContentKind Kind,
    string? MimeType,
    long? SizeBytes,
    ContentStatus Status,
    bool Stored);
