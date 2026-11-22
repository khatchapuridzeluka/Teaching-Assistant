using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentStrengthFilter
{
    public string? AssignmentId { get; init; }
    public MistakeCategory? Category { get; init; }
    public StrengthLabel? Label { get; init; }
}
