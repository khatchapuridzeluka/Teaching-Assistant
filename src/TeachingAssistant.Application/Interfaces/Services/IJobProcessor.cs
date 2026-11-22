namespace TeachingAssistant.Application.Interfaces.Services;

public interface IJobProcessor
{
    Task<ClaimedJob?> ClaimNextAsync(string workerId, TimeSpan lease, CancellationToken cancellationToken = default);

    Task<bool> HeartbeatAsync(Guid jobId, TimeSpan lease, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid jobId, string? result, CancellationToken cancellationToken = default);

    Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default);

    Task ConfirmCanceledAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default);

    Task<int> ReconcileGroupsAsync(CancellationToken cancellationToken = default);
}
