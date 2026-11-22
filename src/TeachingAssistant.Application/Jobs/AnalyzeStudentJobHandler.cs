using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class AnalyzeStudentJobHandler : IJobHandler
{
    private readonly IStudentAnalysisService _analysis;

    public AnalyzeStudentJobHandler(IStudentAnalysisService analysis)
    {
        _analysis = analysis;
    }

    public JobType Type => JobType.AnalyzeStudent;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<AnalyzeStudentPayload>(payload);
        var analysisId = await _analysis.AnalyzeAsync(input.CourseId, input.StudentId, input.Kind, cancellationToken);
        return JobPayloads.Write(new AnalyzeStudentResult { AnalysisId = analysisId });
    }
}
