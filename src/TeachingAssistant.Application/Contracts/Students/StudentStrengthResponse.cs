using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentStrengthResponse(
    Guid Id,
    string AssignmentId,
    string? AssignmentTitle,
    DateTimeOffset? AssignmentDueAt,
    Guid VersionId,
    Guid? ReportId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeCategory Category,
    string Message,
    MistakeDecision Decision,
    StrengthLabel Label,
    string? SinceAssignmentTitle,
    DateTimeOffset ApprovedAt,
    StudentRecordSource Source);
