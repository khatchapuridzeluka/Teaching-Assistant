using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingGuidanceResponse(
    string? CourseGuidance,
    DateTimeOffset? CourseGuidanceUpdatedAt,
    IReadOnlyList<GradingPrincipleResponse> Approach,
    IReadOnlyList<PolicyRuleResponse> Rules);

public record GradingPrincipleResponse(Guid Id, string Title, string Detail, DateTimeOffset UpdatedAt);

public record GradingPrincipleRequest(string Title, string Detail);

public record CourseGuidanceRequest(string? Text);

public record PolicyRuleResponse(string Title, PolicyEffect Effect, double? Points, string? PointsPer, string Covers, string Detail);
