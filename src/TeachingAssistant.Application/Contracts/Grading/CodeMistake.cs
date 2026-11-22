using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record CodeMistake(
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    Guid? Id = null,
    double? Points = null);
