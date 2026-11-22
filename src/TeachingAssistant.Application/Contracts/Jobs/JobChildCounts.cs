namespace TeachingAssistant.Application.Contracts;

public record JobChildCounts(int Total, int Queued, int Running, int Succeeded, int Failed, int Canceled);
