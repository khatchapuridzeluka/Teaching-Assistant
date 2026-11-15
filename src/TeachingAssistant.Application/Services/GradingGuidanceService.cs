using System.Text.RegularExpressions;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// What a course is graded with. For now every course uses the built-in approach and rules (read-only); the course's own
/// guidance text and assignment notes come next, stored per course.
/// </summary>
public partial class GradingGuidanceService : IGradingGuidanceService
{
    public Task<GradingGuidanceResponse> GetAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var approach = BuiltInGradingGuidance.Approach
            .Select(p => new GradingPrincipleResponse(p.Title, OneLine(p.Detail)))
            .ToList();
        var rules = BuiltInGradingGuidance.Rules
            .Select(r => new PolicyRuleResponse(r.Title, r.Effect, r.Points, r.PointsPer, r.Covers, OneLine(r.Detail)))
            .ToList();
        return Task.FromResult(new GradingGuidanceResponse(null, approach, rules));
    }

    // The prompt text is wrapped for the model; the page shows it as one paragraph.
    private static string OneLine(string text)
    {
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
