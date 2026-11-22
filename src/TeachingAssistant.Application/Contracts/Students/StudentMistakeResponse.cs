using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentMistakeResponse(
    Guid Id,
    string AssignmentId,
    string? AssignmentTitle,
    DateTimeOffset? AssignmentDueAt,
    Guid VersionId,
    Guid? ReportId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points,
    MistakeDecision Decision,
    MistakeLabel Label,
    string? RepeatedFromAssignmentTitle,
    DateTimeOffset ApprovedAt,
    StudentRecordSource Source);
