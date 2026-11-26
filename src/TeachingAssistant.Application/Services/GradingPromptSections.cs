using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Turns the stored principles, the course's guidance and the stored rules into the grading prompt's sections. The grading
/// prompt and the Rubric and policy page read the same rows, so what the teacher reads is exactly what the grader gets.
/// </summary>
public static class GradingPromptSections
{
    private const string CourseGuidanceHeading =
        "Course guidance (the teacher's own words for this course; follow it where it is more specific than the rules above):";

    private const string PolicyHeading = "Grading policy (the teacher's own rules; they override the general rules above):";

    private const string NotesHeading =
        "Assignment notes (the teacher's notes for this assignment only; where they differ from anything above, they win):";

    /// <summary>
    /// The "How to grade", "Course guidance", "Grading policy" and "Assignment notes" sections, in the order the layers
    /// apply. A section without content is left out.
    /// </summary>
    public static string Build(
        IReadOnlyList<GradingPrinciple> principles, string? courseGuidance, IReadOnlyList<PolicyRule> rules,
        IReadOnlyList<AssignmentNote>? notes = null)
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

        if (rules.Count > 0)
        {
            sections.Add(PolicyHeading + "\n" + string.Join("\n", rules.Select(r => "- " + r.Detail)));
        }

        if (Notes(notes) is { } section)
        {
            sections.Add(section);
        }

        return string.Join("\n\n", sections);
    }

    /// <summary>
    /// The "Assignment notes" section alone, or null when there are none. Grading sends it as its own block after the
    /// rest of the system prompt, so the part before it is the same for every assignment of the course and can be cached.
    /// </summary>
    public static string? Notes(IReadOnlyList<AssignmentNote>? notes)
    {
        return notes is { Count: > 0 } ? NotesHeading + "\n" + string.Join("\n", notes.Select(n => "- " + n.Detail)) : null;
    }
}
