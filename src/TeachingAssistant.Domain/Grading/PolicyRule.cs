namespace TeachingAssistant.Domain.Grading;

public enum PolicyEffect
{
    /// <summary>Listed as a Style suggestion; costs no points.</summary>
    NoDeduction,

    /// <summary>Costs a fixed number of points, for example per occurrence.</summary>
    Deduction,
}

/// <summary>
/// One of the teacher's grading rules: a judgement call the grader must apply the same way every time. Title, effect and
/// scope are for people; Detail is the exact instruction the grader gets.
/// </summary>
public sealed record PolicyRule(string Title, PolicyEffect Effect, double? Points, string? PointsPer, string Covers, string Detail);

/// <summary>A principle of how the grader works, the same for every course: a short title and the exact instruction.</summary>
public sealed record GradingPrinciple(string Title, string Detail);
