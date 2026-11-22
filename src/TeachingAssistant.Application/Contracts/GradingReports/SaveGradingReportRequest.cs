namespace TeachingAssistant.Application.Contracts;

public record SaveGradingReportRequest
{
    public double? Score { get; init; }
    public double? Bonus { get; init; }
    public required string Review { get; init; }
    public required IReadOnlyList<ReportMistakeInput> Mistakes { get; init; }
    public IReadOnlyList<ReportStrengthInput>? Strengths { get; init; }
}
