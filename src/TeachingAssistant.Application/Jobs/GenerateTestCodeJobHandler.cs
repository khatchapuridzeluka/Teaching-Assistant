using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class GenerateTestCodeJobHandler : IJobHandler
{
    private readonly ITestCodeService _testCode;

    public GenerateTestCodeJobHandler(ITestCodeService testCode)
    {
        _testCode = testCode;
    }

    public JobType Type => JobType.GenerateTestCode;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<GenerateTestCodePayload>(payload);
        await _testCode.GenerateAsync(input.TestSetId, cancellationToken);
        return null;
    }
}
