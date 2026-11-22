using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingGuidanceResponse
{
    public string? CourseGuidance { get; init; }
    public DateTimeOffset? CourseGuidanceUpdatedAt { get; init; }
    public required IReadOnlyList<GradingPrincipleResponse> Approach { get; init; }
    public required IReadOnlyList<PolicyRuleResponse> Rules { get; init; }
    public required IReadOnlyList<AssignmentNoteCount> Notes { get; init; }
}
