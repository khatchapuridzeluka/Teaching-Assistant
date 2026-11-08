using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public interface IJobHandler
{
    JobType Type { get; }

    Task<string?> RunAsync(string payload, CancellationToken cancellationToken);
}
