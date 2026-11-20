using System.Diagnostics;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

/// <summary>
/// One side grading of an evaluation run: grades the report's version with the run's model exactly as a draft would be
/// graded, and stores the result with the run instead of as a draft. A failure is recorded on the result and rethrown, so
/// the job retries like any grading and a later success replaces it.
/// </summary>
public class EvaluationGradeJobHandler : IJobHandler
{
    private readonly IEvaluationRunStore _runs;
    private readonly IGradingService _grading;
    private readonly TimeProvider _timeProvider;

    public EvaluationGradeJobHandler(IEvaluationRunStore runs, IGradingService grading, TimeProvider timeProvider)
    {
        _runs = runs;
        _grading = grading;
        _timeProvider = timeProvider;
    }

    public JobType Type => JobType.EvaluationGrade;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<EvaluationGradePayload>(payload);
        var result = await _runs.FindResultAsync(input.ResultId, cancellationToken)
                     ?? throw new InvalidRequestException($"Evaluation result {input.ResultId} no longer exists.");
        if (result.Status == Domain.Grading.RunResultStatus.Done)
        {
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var grading = await _grading.GradeForEvaluationAsync(result.VersionId, result.Model, cancellationToken)
                          ?? throw new InvalidRequestException("The graded version no longer exists.");
            result.Complete(grading.AnsweredModel, grading.Score, grading.MaxScore, grading.Bonus, grading.Mistakes, grading.BuildStatus,
                Stopwatch.GetElapsedTime(started), _timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Fail(ex.Message, _timeProvider.GetUtcNow());
            await _runs.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        await _runs.SaveChangesAsync(cancellationToken);
        return null;
    }
}
