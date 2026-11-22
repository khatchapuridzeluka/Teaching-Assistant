using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record CodeStrength
{
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public Guid? Id { get; init; }
}
