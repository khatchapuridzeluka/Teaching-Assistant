using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// The grading approach and policy every grading uses today, the same for every course: the principles the grader follows
/// and the teacher's rules on top of them. The grading prompt is built from these, and the Rubric and policy page shows
/// them, so what the teacher reads is exactly what the grader gets. Editable course guidance and assignment notes will
/// be added on top of these.
/// </summary>
public static class BuiltInGradingGuidance
{
    private const string PolicyHeading = "Grading policy (the teacher's own rules; they override the general rules above):";

    public static IReadOnlyList<GradingPrinciple> Approach { get; } =
    [
        new("Follow the points in the instructions",
            """
            The assignment's instructions (the attached instruction files and the task statement) define the tasks and how
              many points each is worth. Follow that breakdown exactly. If the instructions give no points, split the
              assignment's maximum points across the tasks sensibly and say that you did.
            """),
        new("Judge by reading the code",
            """
            You only see the source code; it has not been compiled or run. Judge behaviour by reading the code carefully.
              When the instructions give an expected output, trace the code to check whether it would produce the same
              values and behaviour, and say "likely" when you cannot be sure. Small differences in the printed text itself
              do not count (see the grading policy below).
            """),
        new("Deduct only for real problems",
            """
            Deduct points only for real problems measured against the instructions: missing or wrong functionality, rules
              the instructions state (for example that fields must be private), and clear design mistakes for this course
              level. Do not deduct for style choices the instructions do not mention, and do not invent requirements.
            """),
        new("A different valid solution earns full points",
            """
            A requirement met in a different but valid way (other variable names, extra helper methods) earns full points,
              unless the instructions fix the exact form.
            """),
        new("A bonus only for a finished bonus task",
            """
            Award a bonus only when its task is actually done, and show it separately from the base total.
            """),
        new("Don't deduct for files it could not see",
            """
            Some submitted files may be listed as not included; do not deduct points for content you could not see, but
              mention it.
            """),
    ];

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

    /// <summary>The "How to grade" and "Grading policy" sections of the grading prompt.</summary>
    public static string PromptSections()
    {
        return "How to grade:\n" + string.Join("\n", Approach.Select(p => "- " + p.Detail))
            + "\n\n" + PolicyHeading + "\n" + string.Join("\n", Rules.Select(r => "- " + r.Detail));
    }
}
