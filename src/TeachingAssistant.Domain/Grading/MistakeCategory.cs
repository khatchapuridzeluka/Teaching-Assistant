namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// What kind of mistake it is. A fixed set, never free text, so mistakes can be clustered across a class and tracked
/// per student over time.
/// </summary>
public enum MistakeCategory
{
    /// <summary>The code does the wrong thing or would not compile.</summary>
    Correctness,

    /// <summary>A task or requirement from the instructions is missing or only partly done.</summary>
    MissingRequirement,

    /// <summary>Fields or internals exposed that should be hidden (for example public fields).</summary>
    Encapsulation,

    /// <summary>Class design, inheritance, interfaces or design patterns used badly.</summary>
    Design,

    /// <summary>Exceptions, invalid input or edge cases not handled.</summary>
    ErrorHandling,

    /// <summary>Unclear names of classes, methods or variables.</summary>
    Naming,

    /// <summary>Hard-to-read code: duplication, long methods, dead code.</summary>
    Readability,

    Other,
}
