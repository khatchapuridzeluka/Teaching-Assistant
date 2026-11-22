namespace TeachingAssistant.Application.Contracts;

public record JobChildCounts
{
    public required int Total { get; init; }
    public required int Queued { get; init; }
    public required int Running { get; init; }
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
    public required int Canceled { get; init; }
}
