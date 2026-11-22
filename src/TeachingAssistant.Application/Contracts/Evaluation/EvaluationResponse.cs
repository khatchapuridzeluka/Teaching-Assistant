namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// How the AI's drafts compare with the teacher's approved reports made from them. Scores are compared as a share of the
/// assignment's maximum, so assignments out of 50 and 100 count alike. Only approved reports of drafts that were not
/// discarded count: they are the teacher's final word. Discarded drafts are listed apart with why they were discarded,
/// since a draft thrown away as poor grading is the clearest failure of all.
/// </summary>
public record EvaluationResponse
{
    /// <summary>How many approved reports were compared.</summary>
    public required int Reports { get; init; }

    /// <summary>How close the AI's totals (score plus bonus) came to the teacher's.</summary>
    public required EvaluationScores Scores { get; init; }

    /// <summary>What the teacher did with the AI's mistakes.</summary>
    public required EvaluationMistakes Mistakes { get; init; }

    /// <summary>What the teacher did with the AI's strengths.</summary>
    public required EvaluationStrengths Strengths { get; init; }

    /// <summary>The AI's totals against the grades returned in Classroom, an independent check.</summary>
    public required EvaluationClassroom Classroom { get; init; }

    /// <summary>The same numbers per model that wrote the drafts.</summary>
    public required IReadOnlyList<EvaluationModel> Models { get; init; }

    /// <summary>The discarded drafts, counted by reason and listed with their context.</summary>
    public required EvaluationDiscards Discards { get; init; }

    /// <summary>One row per compared report, largest disagreement first.</summary>
    public required IReadOnlyList<EvaluationRow> Rows { get; init; }

    /// <summary>
    /// Null for the drafts you reviewed; otherwise the model whose side gradings (evaluation runs) are compared with your
    /// approved reports instead. Then Mistakes counts the model's mistakes that match yours (kept: matches one you kept,
    /// rejected: repeats one you rejected), Found says which of your mistakes it found, and Strengths, Models and Discards,
    /// which are about drafts, are empty.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>Only for a model: how many of your kept mistakes its side gradings found.</summary>
    public EvaluationFound? Found { get; init; }
}
