using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Contracts;

public record JobResponse(
    Guid Id,
    JobType Type,
    JobStatus Status,
    Guid? VersionId,
    Guid? GradingId,
    int? ProgressDone,
    int? ProgressTotal,
    string? Error,
    int Attempts,
    int MaxAttempts,
    bool CancelRequested,
    DateTimeOffset RunAfter,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);
