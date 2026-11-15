namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// The teacher's own words on how to grade one course: the level of the group, how strict to be, what matters most and the
/// tone of the feedback. The grader reads it after the built-in principles and before the rules.
/// </summary>
public class CourseGuidance
{
    public const int MaxTextLength = 4000;

    private CourseGuidance()
    {
    }

    public CourseGuidance(string courseId, string text, DateTimeOffset at)
    {
        CourseId = courseId;
        Text = text.Trim();
        UpdatedAt = at.ToUniversalTime();
    }

    public string CourseId { get; private set; } = null!;

    public string Text { get; private set; } = "";

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Change(string text, DateTimeOffset at)
    {
        Text = text.Trim();
        UpdatedAt = at.ToUniversalTime();
    }
}
