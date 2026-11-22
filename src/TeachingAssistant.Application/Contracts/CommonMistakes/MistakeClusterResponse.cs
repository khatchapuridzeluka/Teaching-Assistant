namespace TeachingAssistant.Application.Contracts;

public record MistakeClusterResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Explanation { get; init; }
    public string? ApprovedExplanation { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public required bool Ungrouped { get; init; }
    public required int Students { get; init; }
    public required double StudentPercent { get; init; }
    public required int Count { get; init; }
    public required double PointsLost { get; init; }
    public double? AveragePoints { get; init; }
    public required int Major { get; init; }
    public required int Minor { get; init; }
    public required int Style { get; init; }
    public required IReadOnlyList<ClusterMemberResponse> Members { get; init; }
}
