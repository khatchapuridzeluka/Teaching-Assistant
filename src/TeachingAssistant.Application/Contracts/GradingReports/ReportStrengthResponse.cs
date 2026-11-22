using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReportStrengthResponse
{
    public required Guid Id { get; init; }
    public Guid? SourceStrengthId { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public required MistakeDecision Decision { get; init; }
}
