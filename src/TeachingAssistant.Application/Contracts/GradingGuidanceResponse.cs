using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingGuidanceResponse(
    string? CourseGuidance,
    DateTimeOffset? CourseGuidanceUpdatedAt,
    IReadOnlyList<GradingPrincipleResponse> Approach,
    IReadOnlyList<PolicyRuleResponse> Rules,
    IReadOnlyList<AssignmentNoteCount> Notes);

public record GradingPrincipleResponse(Guid Id, string Title, string Detail, DateTimeOffset UpdatedAt);

public record GradingPrincipleRequest(string Title, string Detail);

public record CourseGuidanceRequest(string? Text);

public record PolicyRuleResponse(
    Guid Id,
    string Title,
    PolicyEffect Effect,
    double? Points,
    string? PointsPer,
    string Covers,
    string Detail,
    DateTimeOffset UpdatedAt);

public record PolicyRuleRequest(string Title, PolicyEffect Effect, double? Points, string? PointsPer, string Covers, string Detail);
