namespace TeachingAssistant.Application.Contracts;

/// <param name="ApprovedReports">Approved reports that count; Reports says how many of them this model has graded.</param>
/// <param name="TeacherMistakes">The mistakes you kept in the graded reports.</param>
/// <param name="Found">How many of them the model found again (same file and overlapping lines, or the same category for a whole-submission mistake).</param>
/// <param name="RepeatedFalseAlarms">The model's mistakes that repeat one you rejected.</param>
/// <param name="Unreviewed">The model's mistakes that match nothing in your report, so nobody has judged them.</param>
public record EvaluationFound(
    int ApprovedReports,
    int TeacherMistakes,
    int Found,
    int RepeatedFalseAlarms,
    int Unreviewed,
    IReadOnlyList<CategoryFound> ByCategory,
    IReadOnlyList<SeverityFound> BySeverity);
