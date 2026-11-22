using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradeChange(GradeEventKind Kind, double? PointsEarned, double? MaxPoints, DateTimeOffset At);
