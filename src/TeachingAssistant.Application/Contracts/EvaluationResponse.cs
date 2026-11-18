using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// How the AI's drafts compare with the teacher's approved reports made from them. Scores are compared as a share of the
/// assignment's maximum, so assignments out of 50 and 100 count alike. Only approved reports count: they are the teacher's
/// final word, and discarded or unreviewed drafts say nothing about whether the AI was right.
/// </summary>
/// <param name="Reports">How many approved reports were compared.</param>
/// <param name="Scores">How close the AI's totals (score plus bonus) came to the teacher's.</param>
/// <param name="Mistakes">What the teacher did with the AI's mistakes.</param>
/// <param name="Strengths">What the teacher did with the AI's strengths.</param>
/// <param name="Classroom">The AI's totals against the grades returned in Classroom, an independent check.</param>
/// <param name="Models">The same numbers per model that wrote the drafts.</param>
/// <param name="Rows">One row per compared report, largest disagreement first.</param>
public record EvaluationResponse(
    int Reports,
    EvaluationScores Scores,
    EvaluationMistakes Mistakes,
    EvaluationStrengths Strengths,
    EvaluationClassroom Classroom,
    IReadOnlyList<EvaluationModel> Models,
    IReadOnlyList<EvaluationRow> Rows);

/// <param name="Compared">Reports where both totals and the maximum are known.</param>
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

public record EvaluationModel(
    string Model,
    int Reports,
    int ScoresCompared,
    int WithinTolerance,
    double? MeanAbsoluteDifference,
    int Mistakes,
    int MistakesRejected);

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
