namespace TeachingAssistant.Application.Contracts;

public record MistakeAssignmentResponse
{
    public required string AssignmentId { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public required int Total { get; init; }
    public required int Major { get; init; }
    public required int Minor { get; init; }
    public required int Style { get; init; }
    public required int Repeated { get; init; }
    public required double PointsLost { get; init; }
    public double? MaxPoints { get; init; }
    public double? ClassroomGrade { get; init; }
    public double? ReportScore { get; init; }
    public double? ReportBonus { get; init; }
    public double? AiScore { get; init; }
    public double? AiBonus { get; init; }
    public required StudentRecordSource Source { get; init; }
}
