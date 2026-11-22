using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record StrengthCategoryResponse(
    MistakeCategory Category,
    int Count,
    double Percent,
    int Assignments,
    bool Consistent,
    int Mistakes,
    string? ImprovedSinceAssignmentTitle,
    string? FirstAssignmentTitle,
    string? LastAssignmentTitle);
