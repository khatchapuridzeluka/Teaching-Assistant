using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record StudentMistakeResponse(
    Guid Id,
    string AssignmentId,
    Guid VersionId,
    Guid ReportId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points,
    MistakeDecision Decision,
    DateTimeOffset ApprovedAt);
