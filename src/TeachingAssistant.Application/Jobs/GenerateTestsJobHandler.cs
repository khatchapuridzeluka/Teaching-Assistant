using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class GenerateTestsJobHandler : IJobHandler
{
    private readonly ITestDesignService _tests;

    public GenerateTestsJobHandler(ITestDesignService tests)
    {
        _tests = tests;
    }

    public JobType Type => JobType.GenerateTests;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<GenerateTestsPayload>(payload);
        var testSetId = await _tests.GenerateAsync(input.CourseId, input.AssignmentId, input.Request, cancellationToken);
        return JobPayloads.Write(new GenerateTestsResult { TestSetId = testSetId });
    }
}
