namespace TeachingAssistant.Application.Models;

public record ReportScore
{
    public required Guid ReportId { get; init; }
    public double? Score { get; init; }
    public double? Bonus { get; init; }
    public double? AiScore { get; init; }
    public double? AiBonus { get; init; }
    public double? MaxScore { get; init; }
}
