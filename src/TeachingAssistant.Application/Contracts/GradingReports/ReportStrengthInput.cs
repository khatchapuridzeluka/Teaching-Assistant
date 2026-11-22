using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReportStrengthInput(
    Guid SourceStrengthId,
    bool Rejected,
    MistakeCategory Category,
    string Message);
