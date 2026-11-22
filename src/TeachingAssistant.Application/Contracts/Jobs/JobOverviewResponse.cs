namespace TeachingAssistant.Application.Contracts;

public record JobOverviewResponse
{
    public required JobCounts Counts { get; init; }
    public required IReadOnlyList<JobOverviewItem> Jobs { get; init; }
}
