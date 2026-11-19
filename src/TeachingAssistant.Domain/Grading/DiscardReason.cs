namespace TeachingAssistant.Domain.Grading;

/// <summary>Why a draft was thrown away. Only PoorGrading says the AI got it wrong; the others are about the workflow.</summary>
public enum DiscardReason
{
    /// <summary>The teacher found the grading wrong or poor against the instructions and the rules.</summary>
    PoorGrading,

    /// <summary>The wrong version or files were graded.</summary>
    WrongSubmission,

    /// <summary>Not needed: a test, or graded again for another reason.</summary>
    NotNeeded,

    /// <summary>Another draft of the same submission was approved, so this one was set aside automatically.</summary>
    Superseded,

    /// <summary>Another reason, given in the note.</summary>
    Other,
}
