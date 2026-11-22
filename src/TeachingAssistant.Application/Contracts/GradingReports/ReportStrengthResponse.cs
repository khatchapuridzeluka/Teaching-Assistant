using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReportStrengthResponse(
    Guid Id,
    Guid? SourceStrengthId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeCategory Category,
    string Message,
    MistakeDecision Decision);
