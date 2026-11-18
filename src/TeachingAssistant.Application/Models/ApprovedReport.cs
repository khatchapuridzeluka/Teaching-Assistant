using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

/// <summary>An approved report together with the AI draft it was made from, with both their mistakes and strengths.</summary>
public record ApprovedReport(GradingReport Report, AiGrading Draft);
