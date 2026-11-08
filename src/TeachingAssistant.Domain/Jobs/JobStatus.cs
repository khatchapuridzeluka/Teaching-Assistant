namespace TeachingAssistant.Domain.Jobs;

/// <summary>
/// Where a job is in its life: Queued → Running → Succeeded, Failed or Canceled. A failed attempt that may succeed later
/// goes back to Queued with a later run time.
/// </summary>
public enum JobStatus
{
    /// <summary>Waiting for a worker (new, or retrying after a temporary failure).</summary>
    Queued,

    /// <summary>A worker is running it.</summary>
    Running,

    /// <summary>Done; the result points at what it produced.</summary>
    Succeeded,

    /// <summary>Gave up: a permanent error, or temporary errors on every attempt.</summary>
    Failed,

    /// <summary>Stopped by the teacher before it finished.</summary>
    Canceled,
}
