namespace TeachingAssistant.Application.Contracts;

public record GradingPrincipleResponse(Guid Id, string Title, string Detail, DateTimeOffset UpdatedAt);
