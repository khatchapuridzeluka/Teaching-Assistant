namespace TeachingAssistant.Application.Contracts;

public record GradebookColumn(
    string Id,
    string Title,
    double? MaxPoints,
    DateTimeOffset? DueAt,
    int Graded,
    int AwaitingGrade,
    int Missing,
    double? AveragePercent);
