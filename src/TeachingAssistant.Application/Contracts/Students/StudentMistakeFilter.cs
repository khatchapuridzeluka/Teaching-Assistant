using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentMistakeFilter(
    string? AssignmentId = null,
    MistakeCategory? Category = null,
    MistakeSeverity? Severity = null,
    MistakeLabel? Label = null);
