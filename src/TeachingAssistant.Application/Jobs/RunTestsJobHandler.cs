using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class RunTestsJobHandler : IJobHandler
{
    private readonly ITestRunService _testRuns;

    public RunTestsJobHandler(ITestRunService testRuns)
    {
        _testRuns = testRuns;
    }

    public JobType Type => JobType.RunTests;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<RunTestsPayload>(payload);
        await _testRuns.RunAsync(input.TestSetId, input.VersionId, input.NewAdapter, cancellationToken);
        return null;
    }
}
