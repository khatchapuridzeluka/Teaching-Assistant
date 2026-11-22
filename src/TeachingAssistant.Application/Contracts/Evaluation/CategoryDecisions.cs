using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record CategoryDecisions
{
    public required MistakeCategory Category { get; init; }
    public required int Kept { get; init; }
    public required int Edited { get; init; }
    public required int Rejected { get; init; }
}
