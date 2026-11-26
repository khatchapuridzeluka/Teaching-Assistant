namespace TeachingAssistant.Application.Models;

public abstract record PromptPart
{
    public bool CacheAfter { get; init; }
}
