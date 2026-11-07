namespace TeachingAssistant.Domain.Grading;

/// <summary>What a grade change in the source's history changed.</summary>
public enum GradeEventKind
{
    /// <summary>The teacher typed a grade that the student does not see yet.</summary>
    DraftGrade,

    /// <summary>The grade the student sees: set when the teacher returned the work with a grade.</summary>
    AssignedGrade,

    /// <summary>The assignment's maximum points changed.</summary>
    MaxPoints,

    /// <summary>A change the source reports that we do not know.</summary>
    Other,
}
