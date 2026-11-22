namespace TeachingAssistant.Application.Models;

public record JobChildCount
{
    public required Guid ParentId { get; init; }
    public required Domain.Jobs.JobStatus Status { get; init; }
    public required int Count { get; init; }
}
