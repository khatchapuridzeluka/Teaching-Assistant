namespace TeachingAssistant.Application.Contracts;

public record MistakeClusterResponse(
    Guid Id,
    string Title,
    string Explanation,
    string? ApprovedExplanation,
    DateTimeOffset? ApprovedAt,
    bool Ungrouped,
    int Students,
    double StudentPercent,
    int Count,
    double PointsLost,
    double? AveragePoints,
    int Major,
    int Minor,
    int Style,
    IReadOnlyList<ClusterMemberResponse> Members);
