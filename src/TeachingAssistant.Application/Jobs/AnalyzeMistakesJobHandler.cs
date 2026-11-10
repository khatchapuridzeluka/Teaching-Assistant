using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class AnalyzeMistakesJobHandler : IJobHandler
{
    private readonly IMistakeAnalysisService _analysis;

    public AnalyzeMistakesJobHandler(IMistakeAnalysisService analysis)
    {
        _analysis = analysis;
    }

    public JobType Type => JobType.AnalyzeMistakes;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<AnalyzeMistakesPayload>(payload);
        var analysisId = await _analysis.AnalyzeAsync(input.CourseId, input.AssignmentId, cancellationToken);
        return JobPayloads.Write(new AnalyzeMistakesResult(analysisId));
    }
}
