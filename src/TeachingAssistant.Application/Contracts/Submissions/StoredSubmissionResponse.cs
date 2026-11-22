namespace TeachingAssistant.Application.Contracts;

public record StoredSubmissionResponse
{
    public required Guid Id { get; init; }
    public required string ExternalSubmissionId { get; init; }
    public required string StudentName { get; init; }
    public required IReadOnlyList<StoredVersionResponse> Versions { get; init; }
}
