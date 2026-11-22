namespace TeachingAssistant.Application.Contracts;

public record GradebookColumn
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public double? MaxPoints { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public required int Graded { get; init; }
    public required int AwaitingGrade { get; init; }
    public required int Missing { get; init; }
    public double? AveragePercent { get; init; }
}
