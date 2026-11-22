namespace TeachingAssistant.Application.Contracts;

public record MistakeShare<T>(T Key, int Count, double Percent);
