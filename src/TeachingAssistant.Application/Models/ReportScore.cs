namespace TeachingAssistant.Application.Models;

public record ReportScore(
    Guid ReportId,
    double? Score,
    double? Bonus,
    double? AiScore,
    double? AiBonus,
    double? MaxScore);
