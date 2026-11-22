namespace TeachingAssistant.Domain.Grading;

public sealed record ReportStrengthEdit
{
    public required Guid SourceStrengthId { get; init; }
    public required bool Rejected { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
}
