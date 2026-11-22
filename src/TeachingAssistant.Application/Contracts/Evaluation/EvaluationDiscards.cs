namespace TeachingAssistant.Application.Contracts;

/// <param name="Total">Every discarded draft.</param>
/// <param name="PoorGrading">Discarded because the grading was wrong or poor: the AI's clearest failures.</param>
/// <param name="ByReason">How many per reason; a null reason means discarded before reasons were recorded.</param>
/// <param name="Rows">Every discarded draft, newest discard first.</param>
public record EvaluationDiscards(int Total, int PoorGrading, IReadOnlyList<DiscardReasonCount> ByReason, IReadOnlyList<DiscardedRow> Rows);
