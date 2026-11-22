using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Contracts;

public record JobResponse
{
    public required Guid Id { get; init; }
    public required JobType Type { get; init; }
    public required JobStatus Status { get; init; }
    public Guid? VersionId { get; init; }
    public Guid? GradingId { get; init; }
    public int? ProgressDone { get; init; }
    public int? ProgressTotal { get; init; }
    public string? Error { get; init; }
    public required int Attempts { get; init; }
    public required int MaxAttempts { get; init; }
    public required bool CancelRequested { get; init; }
    public required DateTimeOffset RunAfter { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public Guid? ParentId { get; init; }
    public string? Summary { get; init; }
}
