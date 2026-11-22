using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record PolicyRuleRequest(string Title, PolicyEffect Effect, double? Points, string? PointsPer, string Covers, string Detail);
