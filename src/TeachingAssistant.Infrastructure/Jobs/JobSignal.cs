using TeachingAssistant.Application.Interfaces;

namespace TeachingAssistant.Infrastructure.Jobs;

public class JobSignal : IJobSignal
{
    private readonly SemaphoreSlim _bell = new(0, 1);

    public void Notify()
    {
        try
        {
            _bell.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _bell.WaitAsync(timeout, cancellationToken);
}
