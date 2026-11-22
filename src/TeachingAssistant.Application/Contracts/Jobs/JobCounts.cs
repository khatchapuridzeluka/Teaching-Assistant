namespace TeachingAssistant.Application.Contracts;

public record JobCounts(int Queued, int Running, int Succeeded, int Failed, int Canceled, DateTimeOffset FinishedSince);
