namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// The models compared on the same homework: each approved report graded again on the side by every model of an evaluation
/// run, against the teacher's report. Nothing a run produces is a draft.
/// </summary>
public record ModelComparisonResponse
{
    /// <summary>The models a run can use.</summary>
    public required IReadOnlyList<string> AvailableModels { get; init; }

    /// <summary>The model normal gradings use now (the Anthropic:GradingModel setting).</summary>
    public required string GradingModel { get; init; }

    /// <summary>How many approved reports a new run would grade again, per model.</summary>
    public required int ApprovedReports { get; init; }

    /// <summary>The newest run with its progress; null before the first run.</summary>
    public EvaluationRunSummary? LatestRun { get; init; }

    /// <summary>One row per model, from each report's newest finished result.</summary>
    public required IReadOnlyList<ModelComparisonRow> Models { get; init; }
}
