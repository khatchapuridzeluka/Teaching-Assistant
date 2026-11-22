using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentAssignmentResult
{
    public required string AssignmentId { get; init; }
    public string? SubmissionId { get; init; }
    public string? State { get; init; }
    public required bool TurnedIn { get; init; }
    public required bool Late { get; init; }
    public required bool Missing { get; init; }
    public required GradeStatus GradeStatus { get; init; }
    public double? Grade { get; init; }
}
