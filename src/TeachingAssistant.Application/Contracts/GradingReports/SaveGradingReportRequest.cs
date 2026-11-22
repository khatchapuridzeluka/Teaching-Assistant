namespace TeachingAssistant.Application.Contracts;

public record SaveGradingReportRequest(
    double? Score,
    double? Bonus,
    string Review,
    IReadOnlyList<ReportMistakeInput> Mistakes,
    IReadOnlyList<ReportStrengthInput>? Strengths = null);
