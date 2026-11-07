namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// How much a mistake matters. A fixed set, never free text, so mistakes can be counted and compared across students.
/// </summary>
public enum MistakeSeverity
{
    /// <summary>Wrong or missing functionality, or a rule the instructions state; costs points.</summary>
    Major,

    /// <summary>A real but small problem; may cost a few points.</summary>
    Minor,

    /// <summary>Readability or style; worth mentioning, usually no points.</summary>
    Style,
}
