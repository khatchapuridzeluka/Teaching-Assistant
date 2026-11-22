namespace TeachingAssistant.Application.Contracts;

/// <summary>Differences from From up to but not including To; the outer buckets are open-ended.</summary>
public record DifferenceBucket
{
    public required int From { get; init; }
    public required int To { get; init; }
    public required int Count { get; init; }
}
