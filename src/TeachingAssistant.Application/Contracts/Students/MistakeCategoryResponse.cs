using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record MistakeCategoryResponse(
    MistakeCategory Category,
    int Count,
    double Percent,
    int Assignments,
    bool Repeated,
    double PointsLost,
    string? FirstAssignmentTitle,
    string? LastAssignmentTitle);
