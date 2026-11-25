using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

internal static class JobPayloads
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Empty job payload: {json}");

    public static JobResponse ToResponse(Job job)
    {
        Guid? versionId = job.Type switch
        {
            JobType.GradeVersion => Read<GradeVersionPayload>(job.Payload).VersionId,
            JobType.RunTests => Read<RunTestsPayload>(job.Payload).VersionId,
            _ => null,
        };
        Guid? gradingId = job is { Type: JobType.GradeVersion, Result: { } result } ? Read<GradeVersionResult>(result).GradingId : null;

        return new JobResponse
        {
            Id = job.Id,
            Type = job.Type,
            Status = job.Status,
            VersionId = versionId,
            GradingId = gradingId,
            ProgressDone = job.ProgressDone,
            ProgressTotal = job.ProgressTotal,
            Error = job.Error,
            Attempts = job.Attempts,
            MaxAttempts = job.MaxAttempts,
            CancelRequested = job.CancelRequested,
            RunAfter = job.RunAfter,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
            ParentId = job.ParentId,
            Summary = Summary(job),
        };
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
