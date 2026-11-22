namespace TeachingAssistant.Domain.Usage;

public sealed record ModelCallTokens
{
    public required long Input { get; init; }
    public required long Output { get; init; }
    public required long CacheRead { get; init; }
    public required long CacheWrite { get; init; }
}
