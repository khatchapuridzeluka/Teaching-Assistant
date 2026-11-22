using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record PolicyRuleResponse(
    Guid Id,
    string Title,
    PolicyEffect Effect,
    double? Points,
    string? PointsPer,
    string Covers,
    string Detail,
    DateTimeOffset UpdatedAt);
