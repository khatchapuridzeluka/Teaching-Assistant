using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class AgentRunJobHandler : IJobHandler
{
    private readonly IAgentRunService _runs;

    public AgentRunJobHandler(IAgentRunService runs)
    {
        _runs = runs;
    }

    public JobType Type => JobType.AgentRun;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<AgentRunPayload>(payload);
        await _runs.ExecuteAsync(input.RunId, cancellationToken);
        return null;
    }
}
