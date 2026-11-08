namespace TeachingAssistant.Application.Contracts;

public record CourseResponse(
    string Id,
    string Name,
    string? Section,
    string State,
    string? Link);
