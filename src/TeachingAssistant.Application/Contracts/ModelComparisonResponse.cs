namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// The models compared on the same homework: each approved report graded again on the side by every model of an evaluation
/// run, against the teacher's report. Nothing a run produces is a draft.
/// </summary>
/// <param name="AvailableModels">The models a run can use.</param>
/// <param name="GradingModel">The model normal gradings use now (the Anthropic:GradingModel setting).</param>
/// <param name="ApprovedReports">How many approved reports a new run would grade again, per model.</param>
/// <param name="LatestRun">The newest run with its progress; null before the first run.</param>
/// <param name="Models">One row per model, from each report's newest finished result.</param>
public record ModelComparisonResponse(
    IReadOnlyList<string> AvailableModels,
    string GradingModel,
    int ApprovedReports,
    EvaluationRunSummary? LatestRun,
    IReadOnlyList<ModelComparisonRow> Models);

/// <summary>A run and how far it is: its gradings done, failed (each retried a few times first) and still waiting.</summary>
public record EvaluationRunSummary(
    Guid Id,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Models,
    int Total,
    int Done,
    int Failed,
    int Pending);

/// <summary>
/// One model against the teacher's approved reports. Accuracy is 100 minus the average gap between its total and the
/// teacher's, in points per 100. Of the mistakes the teacher kept, Found is how many it found again (matched by file and
/// lines, or category for whole-submission mistakes) and FoundPercent their share; RepeatedFalseAlarms are mistakes it
/// flagged that the teacher had rejected; Unreviewed are mistakes it flagged that match nothing in the report, so nobody has
/// judged them. AverageSeconds is the time per grading, compile check included.
/// </summary>
public record ModelComparisonRow(
    string Model,
    int Graded,
    int ScoresCompared,
    double? Accuracy,
    int WithinTolerance,
    double? MeanDifference,
    int TeacherMistakes,
    int Found,
    double? FoundPercent,
    int RepeatedFalseAlarms,
    int Unreviewed,
    double? AverageSeconds,
    int Failed);

/// <summary>Start an evaluation run: grade the approved reports again with these models, in one course or all (CourseId null).</summary>
public record StartEvaluationRunRequest(IReadOnlyList<string> Models, string? CourseId = null);
