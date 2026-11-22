namespace TeachingAssistant.Application.Contracts;

public record MistakeShare<T>
{
    public required T Key { get; init; }
    public required int Count { get; init; }
    public required double Percent { get; init; }
}
