namespace TeachingAssistant.Application.Contracts;

public record StoredInstructionsResponse
{
    public required Guid VersionId { get; init; }
    public required int Number { get; init; }
    public required int VersionCount { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public double? MaxPoints { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }
    public required IReadOnlyList<InstructionsFileResponse> Files { get; init; }
}
