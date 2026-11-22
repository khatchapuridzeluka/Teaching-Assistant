using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

public record GradingDocumentMistake
{
    public required MistakeSeverity Severity { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public double? Points { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
}
