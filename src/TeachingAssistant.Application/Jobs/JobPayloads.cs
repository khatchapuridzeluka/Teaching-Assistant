using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public record GradeVersionPayload(Guid VersionId);

public record GradeVersionResult(Guid GradingId);

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
            job.FinishedAt);
    }
}
