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
