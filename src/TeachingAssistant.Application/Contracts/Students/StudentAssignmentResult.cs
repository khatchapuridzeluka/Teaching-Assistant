using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentAssignmentResult(
    string AssignmentId,
    string? SubmissionId,
    string? State,
    bool TurnedIn,
    bool Late,
    bool Missing,
    GradeStatus GradeStatus,
    double? Grade);
