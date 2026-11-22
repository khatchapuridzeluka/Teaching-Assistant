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
