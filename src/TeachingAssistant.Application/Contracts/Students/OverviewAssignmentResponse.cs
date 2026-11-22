namespace TeachingAssistant.Application.Contracts;

public record OverviewAssignmentResponse
{
    public required string AssignmentId { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public required StudentRecordSource Source { get; init; }
    public double? Score { get; init; }
    public double? Bonus { get; init; }
    public double? MaxPoints { get; init; }
    public double? Percent { get; init; }
    public double? AiScore { get; init; }
    public required int Mistakes { get; init; }
    public required int Major { get; init; }
    public required int Repeated { get; init; }
    public required double PointsLost { get; init; }
    public required int Strengths { get; init; }
    public required int Improved { get; init; }
}
