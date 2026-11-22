namespace TeachingAssistant.Application.Contracts;

public record EvaluationDiscards
{
    /// <summary>Every discarded draft.</summary>
    public required int Total { get; init; }

    /// <summary>Discarded because the grading was wrong or poor: the AI's clearest failures.</summary>
    public required int PoorGrading { get; init; }

    /// <summary>How many per reason; a null reason means discarded before reasons were recorded.</summary>
    public required IReadOnlyList<DiscardReasonCount> ByReason { get; init; }

    /// <summary>Every discarded draft, newest discard first.</summary>
    public required IReadOnlyList<DiscardedRow> Rows { get; init; }
}
