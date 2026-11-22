namespace TeachingAssistant.Application.Contracts;

public record JobCounts
{
    public required int Queued { get; init; }
    public required int Running { get; init; }
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
    public required int Canceled { get; init; }
    public required DateTimeOffset FinishedSince { get; init; }
}
