using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// The newest stored version of an assignment's instructions: the task statement and materials as they were when
/// fetched, which is exactly what grading sends to the model. VersionCount is how many versions are stored.
/// </summary>
public record StoredInstructionsResponse(
    Guid VersionId,
    int Number,
    int VersionCount,
    string Title,
    string? Description,
    double? MaxPoints,
    DateTimeOffset FetchedAt,
    IReadOnlyList<InstructionsFileResponse> Files);

/// <summary>
/// One material of the instructions. Stored is true when the file is served by
/// GET /courses/{courseId}/assignments/{assignmentId}/instructions/files/{id}; Url is set for links (not stored).
/// </summary>
public record InstructionsFileResponse(
    Guid Id,
    string Name,
    ContentOrigin Origin,
    string? MimeType,
    long? SizeBytes,
    ContentStatus Status,
    bool Stored,
    string? Url);
