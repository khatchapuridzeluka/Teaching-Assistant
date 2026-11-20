using TeachingAssistant.Domain.Grading;

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

/// <summary>Your kept mistakes of one category (as you filed it) and how many the model found.</summary>
public record CategoryFound(MistakeCategory Category, int TeacherMistakes, int Found);

/// <summary>Your kept mistakes of one severity (as you filed it) and how many the model found.</summary>
public record SeverityFound(MistakeSeverity Severity, int TeacherMistakes, int Found);

/// <param name="Total">Every discarded draft.</param>
/// <param name="PoorGrading">Discarded because the grading was wrong or poor: the AI's clearest failures.</param>
/// <param name="ByReason">How many per reason; a null reason means discarded before reasons were recorded.</param>
/// <param name="Rows">Every discarded draft, newest discard first.</param>
public record EvaluationDiscards(int Total, int PoorGrading, IReadOnlyList<DiscardReasonCount> ByReason, IReadOnlyList<DiscardedRow> Rows);

public record DiscardReasonCount(DiscardReason? Reason, int Count);

/// <summary>
/// One discarded draft: whose and which assignment, the model, the AI's total (score plus bonus) and mistakes per severity,
/// why it was discarded and the teacher's note, and the status its report had, if one was made.
/// </summary>
public record DiscardedRow(
    Guid GradingId,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string StudentId,
    string StudentName,
    string Model,
    double? AiScore,
    double? MaxScore,
    int Major,
    int Minor,
    int Style,
    DiscardReason? Reason,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DiscardedAt,
    ReportStatus? ReportStatus);

/// <param name="Compared">Reports where both totals and the maximum are known.</param>
/// <param name="Accuracy">100 minus the average gap, in percent: 97.8 when the AI is off by 2.2 points per 100 on average.</param>
/// <param name="TolerancePercent">How close counts as agreeing, in percent of the maximum (5 = 5 points out of 100).</param>
/// <param name="WithinTolerance">How many of them agree within the tolerance.</param>
/// <param name="Exact">How many have the very same total.</param>
/// <param name="MeanAbsoluteDifference">Average size of the difference, in percent of the maximum.</param>
/// <param name="MeanDifference">Average signed difference, in percent of the maximum: positive means the AI is more lenient.</param>
/// <param name="AiHigher">How many times the AI gave more than the teacher.</param>
/// <param name="AiLower">How many times the AI gave less than the teacher.</param>
/// <param name="Buckets">How the differences spread, in 5-point steps of percent of the maximum.</param>
public record EvaluationScores(
    int Compared,
    double? Accuracy,
    double TolerancePercent,
    int WithinTolerance,
    int Exact,
    double? MeanAbsoluteDifference,
    double? MeanDifference,
    int AiHigher,
    int AiLower,
    IReadOnlyList<DifferenceBucket> Buckets);

/// <summary>Differences from <paramref name="From"/> up to but not including <paramref name="To"/>; the outer buckets are open-ended.</summary>
public record DifferenceBucket(int From, int To, int Count);

/// <param name="Total">The AI's mistakes in the compared reports.</param>
/// <param name="Kept">Kept as the AI wrote them.</param>
/// <param name="Edited">Kept but changed (message, severity, category or points).</param>
/// <param name="Rejected">Rejected: not a real mistake according to the teacher.</param>
/// <param name="PointsChanged">Kept mistakes whose points the teacher changed.</param>
/// <param name="MeanPointsChange">Average size of those point changes.</param>
/// <param name="ByCategory">The decisions per category the AI chose.</param>
/// <param name="BySeverity">The decisions per severity the AI chose.</param>
public record EvaluationMistakes(
    int Total,
    int Kept,
    int Edited,
    int Rejected,
    int PointsChanged,
    double? MeanPointsChange,
    IReadOnlyList<CategoryDecisions> ByCategory,
    IReadOnlyList<SeverityDecisions> BySeverity);

public record CategoryDecisions(MistakeCategory Category, int Kept, int Edited, int Rejected);

public record SeverityDecisions(MistakeSeverity Severity, int Kept, int Edited, int Rejected);

public record EvaluationStrengths(int Total, int Kept, int Edited, int Rejected);

/// <param name="Compared">Reports whose submission has a grade returned in Classroom.</param>
/// <param name="AiWithinTolerance">How many AI totals are within the tolerance of that grade.</param>
/// <param name="AiMeanAbsoluteDifference">Average size of the AI's difference from it, in percent of the maximum.</param>
/// <param name="TeacherWithinTolerance">How many of the teacher's report totals are within the tolerance of it.</param>
public record EvaluationClassroom(
    int Compared,
    int AiWithinTolerance,
    double? AiMeanAbsoluteDifference,
    int TeacherWithinTolerance);

/// <summary>The evaluation numbers for the drafts one model wrote, with how many of its drafts were discarded as poor grading.</summary>
public record EvaluationModel(
    string Model,
    int Reports,
    int ScoresCompared,
    double? Accuracy,
    int WithinTolerance,
    double? MeanAbsoluteDifference,
    int Mistakes,
    int MistakesRejected,
    int DiscardedPoorGrading);

/// <summary>
/// One compared report. Scores are totals (score plus bonus); AiPercent and TeacherPercent are those totals in percent of the
/// maximum, and Difference is the AI's minus the teacher's in percent of the maximum (positive: the AI was more lenient), null
/// when either total is missing. ClassroomGrade is the grade returned in Classroom for the same submission, if any.
/// </summary>
public record EvaluationRow(
    Guid ReportId,
    Guid GradingId,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string StudentId,
    string? StudentName,
    string Model,
    double? AiScore,
    double? TeacherScore,
    double? MaxScore,
    double? AiPercent,
    double? TeacherPercent,
    double? Difference,
    int Mistakes,
    int MistakesEdited,
    int MistakesRejected,
    double? ClassroomGrade,
    DateTimeOffset? ApprovedAt);
