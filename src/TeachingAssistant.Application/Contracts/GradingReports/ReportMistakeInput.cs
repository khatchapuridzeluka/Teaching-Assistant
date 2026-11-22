using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReportMistakeInput(
    Guid SourceMistakeId,
    bool Rejected,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points = null);
