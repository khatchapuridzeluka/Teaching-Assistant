namespace TeachingAssistant.Application.Interfaces.Services;

public interface IAgentRunWaker
{
    Task OnJobFinishedAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<int> SweepAsync(CancellationToken cancellationToken = default);
}
