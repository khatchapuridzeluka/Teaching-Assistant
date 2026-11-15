using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingGuidanceResponse(
    string? CourseGuidance,
    IReadOnlyList<GradingPrincipleResponse> Approach,
    IReadOnlyList<PolicyRuleResponse> Rules);

public record GradingPrincipleResponse(string Title, string Detail);

public record PolicyRuleResponse(string Title, PolicyEffect Effect, double? Points, string? PointsPer, string Covers, string Detail);
