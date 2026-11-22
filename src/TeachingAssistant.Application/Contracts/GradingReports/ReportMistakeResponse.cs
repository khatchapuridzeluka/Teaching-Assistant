using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReportMistakeResponse
{
    public required Guid Id { get; init; }
    public Guid? SourceMistakeId { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public required MistakeSeverity Severity { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public double? Points { get; init; }
    public required MistakeDecision Decision { get; init; }
}
