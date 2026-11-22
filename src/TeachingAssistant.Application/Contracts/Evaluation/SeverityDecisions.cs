using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record SeverityDecisions
{
    public required MistakeSeverity Severity { get; init; }
    public required int Kept { get; init; }
    public required int Edited { get; init; }
    public required int Rejected { get; init; }
}
