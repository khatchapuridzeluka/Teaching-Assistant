namespace TeachingAssistant.Domain.Grading;

public enum PolicyEffect
{
    /// <summary>Listed as a Style suggestion; costs no points.</summary>
    NoDeduction,

    /// <summary>Costs a fixed number of points, for example per occurrence.</summary>
    Deduction,
}
