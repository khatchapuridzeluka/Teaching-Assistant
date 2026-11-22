namespace TeachingAssistant.Application.Contracts;

public record CourseStudentResponse
{
    public required Guid Id { get; init; }
    public required string ExternalUserId { get; init; }
    public required string Name { get; init; }
    public required bool Active { get; init; }
    public required int TurnedIn { get; init; }
    public required int Missing { get; init; }
    public required int Late { get; init; }
    public double? OverallPercent { get; init; }
    public required double Earned { get; init; }
    public required double Possible { get; init; }
    public required IReadOnlyList<StudentAssignmentResult> Results { get; init; }
}
