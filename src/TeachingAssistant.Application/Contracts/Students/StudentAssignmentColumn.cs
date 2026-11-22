namespace TeachingAssistant.Application.Contracts;

public record StudentAssignmentColumn(string Id, string Title, double? MaxPoints, DateTimeOffset? DueAt);
