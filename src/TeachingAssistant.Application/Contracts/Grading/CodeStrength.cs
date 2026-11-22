using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record CodeStrength(
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeCategory Category,
    string Message,
    Guid? Id = null);
