using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentMistakeFilter
{
    public string? AssignmentId { get; init; }
    public MistakeCategory? Category { get; init; }
    public MistakeSeverity? Severity { get; init; }
    public MistakeLabel? Label { get; init; }
}
