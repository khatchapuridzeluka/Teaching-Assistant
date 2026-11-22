namespace TeachingAssistant.Application.Contracts;

public record JobOverviewResponse(JobCounts Counts, IReadOnlyList<JobOverviewItem> Jobs);
