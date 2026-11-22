using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

public record GradingDocumentStrength
{
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
}
