namespace TeachingAssistant.Application.Contracts;

/// <summary>Differences from <paramref name="From"/> up to but not including <paramref name="To"/>; the outer buckets are open-ended.</summary>
public record DifferenceBucket(int From, int To, int Count);
