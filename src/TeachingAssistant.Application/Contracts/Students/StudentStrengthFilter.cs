using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentStrengthFilter(
    string? AssignmentId = null,
    MistakeCategory? Category = null,
    StrengthLabel? Label = null);
