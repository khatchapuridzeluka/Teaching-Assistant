using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record OverviewCategoryResponse(MistakeCategory Category, int Strengths, int Mistakes, double PointsLost);
