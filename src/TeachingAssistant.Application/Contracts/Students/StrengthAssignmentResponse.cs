namespace TeachingAssistant.Application.Contracts;

public record StrengthAssignmentResponse(
    string AssignmentId,
    string? Title,
    DateTimeOffset? DueAt,
    int Strengths,
    int Mistakes);
