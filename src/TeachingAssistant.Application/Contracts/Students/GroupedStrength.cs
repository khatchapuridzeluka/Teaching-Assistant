using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GroupedStrength
{
    public required Guid Id { get; init; }
    public string? AssignmentTitle { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
}
