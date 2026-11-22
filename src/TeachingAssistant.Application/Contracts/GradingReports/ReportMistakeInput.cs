using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReportMistakeInput
{
    public required Guid SourceMistakeId { get; init; }
    public required bool Rejected { get; init; }
    public required MistakeSeverity Severity { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public double? Points { get; init; }
}
