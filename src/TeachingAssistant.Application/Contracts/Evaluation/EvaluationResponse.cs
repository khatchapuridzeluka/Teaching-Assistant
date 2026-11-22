namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// How the AI's drafts compare with the teacher's approved reports made from them. Scores are compared as a share of the
/// assignment's maximum, so assignments out of 50 and 100 count alike. Only approved reports of drafts that were not
/// discarded count: they are the teacher's final word. Discarded drafts are listed apart with why they were discarded,
/// since a draft thrown away as poor grading is the clearest failure of all.
/// </summary>
/// <param name="Reports">How many approved reports were compared.</param>
/// <param name="Scores">How close the AI's totals (score plus bonus) came to the teacher's.</param>
/// <param name="Mistakes">What the teacher did with the AI's mistakes.</param>
/// <param name="Strengths">What the teacher did with the AI's strengths.</param>
/// <param name="Classroom">The AI's totals against the grades returned in Classroom, an independent check.</param>
/// <param name="Models">The same numbers per model that wrote the drafts.</param>
/// <param name="Discards">The discarded drafts, counted by reason and listed with their context.</param>
/// <param name="Rows">One row per compared report, largest disagreement first.</param>
/// <param name="Model">
/// Null for the drafts you reviewed; otherwise the model whose side gradings (evaluation runs) are compared with your
/// approved reports instead. Then Mistakes counts the model's mistakes that match yours (kept: matches one you kept,
/// rejected: repeats one you rejected), Found says which of your mistakes it found, and Strengths, Models and Discards,
/// which are about drafts, are empty.
/// </param>
/// <param name="Found">Only for a model: how many of your kept mistakes its side gradings found.</param>
public record EvaluationResponse(
    int Reports,
    EvaluationScores Scores,
    EvaluationMistakes Mistakes,
    EvaluationStrengths Strengths,
    EvaluationClassroom Classroom,
    IReadOnlyList<EvaluationModel> Models,
    EvaluationDiscards Discards,
    IReadOnlyList<EvaluationRow> Rows,
    string? Model = null,
    EvaluationFound? Found = null);
