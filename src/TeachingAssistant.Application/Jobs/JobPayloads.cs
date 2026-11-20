using TeachingAssistant.Domain.Profiling;
using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public record GradeVersionPayload(Guid VersionId, bool PartOfGradeAll = false);

public record GradeVersionResult(Guid GradingId);

public record GradeAssignmentPayload(string CourseId, string AssignmentId, bool Regrade = false);

public record GradeAssignmentResult(int Succeeded, int Failed, int Canceled);

public record AnalyzeMistakesPayload(string CourseId, string AssignmentId);

public record AnalyzeStudentPayload(
    string CourseId, string StudentId, string? StudentName = null, StudentAnalysisKind Kind = StudentAnalysisKind.Mistakes);

public record GradeStudentPayload(string CourseId, string StudentId, string? StudentName = null, bool Regrade = false);

public record FetchStudentWorkPayload(string CourseId, string StudentId, Guid ParentId, bool Regrade = false);

public record FetchStudentWorkResult(
    int Fetched,
    int Queued,
    int AlreadyDrafted,
    int AlreadyQueued,
    int NoWork,
    int NotFetched,
    IReadOnlyList<string> Problems);

public record AnalyzeMistakesResult(Guid AnalysisId);

public record AnalyzeStudentResult(Guid AnalysisId);

public record AgentRunPayload(Guid RunId);

public record EvaluationRunPayload(Guid RunId, string? CourseId);

public record EvaluationGradePayload(Guid ResultId);

internal static class JobPayloads
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Empty job payload: {json}");

    public static JobResponse ToResponse(Job job)
    {
        Guid? versionId = job.Type == JobType.GradeVersion ? Read<GradeVersionPayload>(job.Payload).VersionId : null;
        Guid? gradingId = job is { Type: JobType.GradeVersion, Result: { } result } ? Read<GradeVersionResult>(result).GradingId : null;

        return new JobResponse(job.Id, job.Type, job.Status, versionId, gradingId, job.ProgressDone, job.ProgressTotal,
            job.Error, job.Attempts, job.MaxAttempts, job.CancelRequested, job.RunAfter, job.CreatedAt, job.StartedAt,
            job.FinishedAt, job.ParentId, Summary(job));
    }

    private static string? Summary(Job job)
    {
        if (job is not { Type: JobType.FetchStudentWork, Result: { } result })
        {
            return null;
        }

        var r = Read<FetchStudentWorkResult>(result);
        var parts = new List<string>
        {
            $"Fetched {r.Fetched} turned-in {(r.Fetched == 1 ? "assignment" : "assignments")} from Classroom",
            $"queued {r.Queued} {(r.Queued == 1 ? "grading" : "gradings")}",
        };
        if (r.AlreadyDrafted > 0)
        {
            parts.Add($"{r.AlreadyDrafted} already had a draft");
        }

        if (r.AlreadyQueued > 0)
        {
            parts.Add($"{r.AlreadyQueued} already queued");
        }

        if (r.NotFetched > 0)
        {
            parts.Add($"{r.NotFetched} still without stored work");
        }

        var summary = string.Join(" · ", parts) + ".";
        return r.Problems.Count == 0 ? summary : $"{summary} Could not be graded: {string.Join(" ", r.Problems)}";
    }
}
