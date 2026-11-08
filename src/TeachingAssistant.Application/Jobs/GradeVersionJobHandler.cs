using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class GradeVersionJobHandler : IJobHandler
{
    private readonly IGradingService _grading;

    public GradeVersionJobHandler(IGradingService grading)
    {
        _grading = grading;
    }

    public JobType Type => JobType.GradeVersion;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<GradeVersionPayload>(payload);
        var grading = await _grading.GradeVersionAsync(input.VersionId, cancellationToken)
                      ?? throw new InvalidRequestException($"Version {input.VersionId} no longer exists.");
        return JobPayloads.Write(new GradeVersionResult(grading.Id));
    }
}
