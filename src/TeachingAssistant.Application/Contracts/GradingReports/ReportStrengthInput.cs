using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReportStrengthInput
{
    public required Guid SourceStrengthId { get; init; }
    public required bool Rejected { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
}
