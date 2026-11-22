using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GroupedStrength(Guid Id, string? AssignmentTitle, MistakeCategory Category, string Message);
