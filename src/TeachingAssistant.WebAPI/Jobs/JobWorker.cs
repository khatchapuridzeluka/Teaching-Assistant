using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Infrastructure.Jobs;

namespace TeachingAssistant.WebAPI.Jobs;

public class JobWorker(
    IServiceScopeFactory scopes,
    JobSignal signal,
    IOptions<JobOptions> options,
    TimeProvider timeProvider,
    ILogger<JobWorker> logger) : BackgroundService
{
    private readonly JobOptions _options = options.Value;
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";
    private DateTimeOffset _lastRecovery = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var running = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                running.RemoveAll(t => t.IsCompleted);

                if (IsRecoveryDue())
                {
                    await RecoverAsync(stoppingToken);
                }

                while (running.Count < _options.MaxConcurrent && await ClaimAsync(stoppingToken) is { } job)
                {
                    running.Add(RunAsync(job, stoppingToken));
                }

                var wake = signal.WaitAsync(_options.Poll, stoppingToken);
                await Task.WhenAny(running.Append(wake));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job worker loop failed; retrying shortly");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await Task.WhenAll(running);
    }

    private async Task<ClaimedJob?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IJobProcessor>()
            .ClaimNextAsync(_workerId, _options.Lease, cancellationToken);
    }

    private bool IsRecoveryDue()
    {
        var sinceLastRecovery = timeProvider.GetUtcNow() - _lastRecovery;
        return sinceLastRecovery >= _options.RecoveryCheck;
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();
        var recovered = await processor.RecoverExpiredLeasesAsync(cancellationToken);
        await processor.ReconcileGroupsAsync(cancellationToken);
        _lastRecovery = timeProvider.GetUtcNow();
        if (recovered > 0)
        {
            logger.LogWarning("Recovered {Count} job(s) whose worker stopped", recovered);
        }
    }

    private async Task RunAsync(ClaimedJob job, CancellationToken stoppingToken)
    {
        using var jobCancel = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var heartbeatStop = new CancellationTokenSource();
        var heartbeat = HeartbeatAsync(job.Id, jobCancel, heartbeatStop.Token);

        try
        {
            logger.LogInformation("Job {JobId} ({Type}) started", job.Id, job.Type);
            string? result;
            await using (var scope = scopes.CreateAsyncScope())
            {
                var handler = scope.ServiceProvider.GetServices<IJobHandler>().Single(h => h.Type == job.Type);
                result = await handler.RunAsync(job.Payload, jobCancel.Token);
            }

            await WithProcessorAsync(p => p.CompleteAsync(job.Id, result, CancellationToken.None));
            logger.LogInformation("Job {JobId} ({Type}) succeeded", job.Id, job.Type);
        }
        catch (OperationCanceledException) when (jobCancel.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            await WithProcessorAsync(p => p.ConfirmCanceledAsync(job.Id, CancellationToken.None));
            logger.LogInformation("Job {JobId} ({Type}) canceled", job.Id, job.Type);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await WithProcessorAsync(p => p.FailAsync(job.Id, new TimeoutException("The API stopped while running this job."), CancellationToken.None));
        }
        catch (Exception ex)
        {
            await WithProcessorAsync(p => p.FailAsync(job.Id, ex, CancellationToken.None));
            logger.LogWarning(ex, "Job {JobId} ({Type}) failed", job.Id, job.Type);
        }
        finally
        {
            await heartbeatStop.CancelAsync();
            await heartbeat;
        }
    }

    private async Task HeartbeatAsync(Guid jobId, CancellationTokenSource jobCancel, CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await Task.Delay(_options.Heartbeat, stop);
                var keepGoing = true;
                await WithProcessorAsync(async p => keepGoing = await p.HeartbeatAsync(jobId, _options.Lease, stop));
                if (!keepGoing)
                {
                    await jobCancel.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Heartbeat for job {JobId} failed", jobId);
        }
    }

    private async Task WithProcessorAsync(Func<IJobProcessor, Task> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IJobProcessor>());
    }
}
