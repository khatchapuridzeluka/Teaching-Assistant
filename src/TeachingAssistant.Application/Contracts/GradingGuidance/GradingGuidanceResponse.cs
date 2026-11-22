using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingGuidanceResponse(
    string? CourseGuidance,
    DateTimeOffset? CourseGuidanceUpdatedAt,
    IReadOnlyList<GradingPrincipleResponse> Approach,
    IReadOnlyList<PolicyRuleResponse> Rules,
    IReadOnlyList<AssignmentNoteCount> Notes);
