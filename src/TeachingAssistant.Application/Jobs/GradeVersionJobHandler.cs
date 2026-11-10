using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class GradeVersionJobHandler : IJobHandler
{
    private readonly IGradingService _grading;
    private readonly ModelCallContext _callContext;

    public GradeVersionJobHandler(IGradingService grading, ModelCallContext callContext)
    {
        _grading = grading;
        _callContext = callContext;
    }

    public JobType Type => JobType.GradeVersion;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<GradeVersionPayload>(payload);
        if (input.PartOfGradeAll)
        {
            _callContext.MarkPartOfGradeAll();
        }

        var grading = await _grading.GradeVersionAsync(input.VersionId, cancellationToken)
                      ?? throw new InvalidRequestException($"Version {input.VersionId} no longer exists.");
        return JobPayloads.Write(new GradeVersionResult(grading.Id));
    }
}
