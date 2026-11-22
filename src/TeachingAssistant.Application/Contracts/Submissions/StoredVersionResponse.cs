using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Contracts;

public record StoredVersionResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required VersionStatus Status { get; init; }
    public string? StatusDetail { get; init; }
    public required bool HasCode { get; init; }
    public string? ExtractionDetail { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }
    public DateTimeOffset? TurnedInAt { get; init; }
    public required IReadOnlyList<StoredFileResponse> Files { get; init; }
}
