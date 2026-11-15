using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// The teacher's grading rules, the same for every course, and how they and the stored principles become the grading
/// prompt. The grading prompt and the Rubric and policy page read the same source, so what the teacher reads is exactly
/// what the grader gets.
/// </summary>
public static class BuiltInGradingGuidance
{
    private const string PolicyHeading = "Grading policy (the teacher's own rules; they override the general rules above):";

    private const string CourseGuidanceHeading =
        "Course guidance (the teacher's own words for this course; follow it where it is more specific than the rules above):";

    public static IReadOnlyList<PolicyRule> Rules { get; } =
    [
        new("Trivial issues cost nothing", PolicyEffect.NoDeduction, null, null,
            "Wording or spacing of printed text, comments, unused imports and unused variables",
            """
            Trivial issues never cost points. The teacher wants students judged on whether their program works and is
              designed well, not punished for polish. Trivial means: the wording, spacing, punctuation, capitalization or
              labels of printed text (as long as the printed values are right); comments (missing, too few, or with typos);
              unused imports; and unused or unnecessary variables. Still list each one in "mistakes" with severity Style, so
              the student sees it as a suggestion, and say in the message that no points were deducted.
            """),
        new("Changing a name the instructions fix", PolicyEffect.Deduction, 3, "per name",
            "A variable or method whose exact name the instructions give, spelled or capitalized differently",
            """
            Names fixed by the instructions: when the instructions specify the exact name of a variable or method and the
              student used a different one, deduct 3 points for each such name (a misspelling or different capitalization
              counts as different). The teacher relies on these names, for example to test the code. List each one with
              severity Minor and say in the message that it cost 3 points and in which task. Names the instructions do not
              fix are the student's choice: a poor name there is a Style suggestion that costs nothing.
            """),
    ];

    /// <summary>
    /// The "How to grade", "Course guidance" and "Grading policy" sections of the grading prompt, in the order the layers
    /// apply: the stored principles, the course's guidance, then the rules. A section without content is left out.
    /// </summary>
    public static string PromptSections(IReadOnlyList<GradingPrinciple> principles, string? courseGuidance = null)
    {
        var sections = new List<string>();
        if (principles.Count > 0)
        {
            sections.Add("How to grade:\n" + string.Join("\n", principles.Select(p => "- " + p.Detail)));
        }

        if (!string.IsNullOrWhiteSpace(courseGuidance))
        {
            sections.Add(CourseGuidanceHeading + "\n" + courseGuidance.Trim());
        }

        if (Rules.Count > 0)
        {
            sections.Add(PolicyHeading + "\n" + string.Join("\n", Rules.Select(r => "- " + r.Detail)));
        }

        return string.Join("\n\n", sections);
    }
}
