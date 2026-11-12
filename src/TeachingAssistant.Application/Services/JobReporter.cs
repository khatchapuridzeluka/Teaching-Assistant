using System.Globalization;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class JobReporter
{
    private readonly IJobStore _jobs;
    private readonly IGradingStore _gradings;

    public JobReporter(IJobStore jobs, IGradingStore gradings)
    {
        _jobs = jobs;
        _gradings = gradings;
    }

    public async Task<string> DescribeAsync(IReadOnlyList<Job> jobs, CancellationToken cancellationToken = default)
    {
        var versionIds = jobs.Where(j => j.Type == JobType.GradeVersion)
            .Select(j => JobPayloads.Read<GradeVersionPayload>(j.Payload).VersionId)
            .Distinct()
            .ToList();
        var subjects = (await _jobs.GetSubjectsAsync(versionIds, cancellationToken)).ToDictionary(s => s.VersionId);

        var lines = new List<string>();
        foreach (var job in jobs)
        {
            lines.Add($"- {await DescribeAsync(job, subjects, cancellationToken)} (job id {job.Id})");
        }

        return string.Join("\n", lines);
    }

    private async Task<string> DescribeAsync(
        Job job, IReadOnlyDictionary<Guid, Models.JobSubject> subjects, CancellationToken cancellationToken)
    {
        if (job.Type == JobType.GradeVersion)
        {
            var versionId = JobPayloads.Read<GradeVersionPayload>(job.Payload).VersionId;
            var who = subjects.GetValueOrDefault(versionId) is { } s
                ? $"{s.StudentName}'s {s.AssignmentTitle ?? "assignment"}, version {s.VersionNumber}"
                : "a submission";
            return job.Status switch
            {
                JobStatus.Succeeded => $"Grading of {who}: draft ready, {await ScoreAsync(job, cancellationToken)}",
                JobStatus.Failed => $"Grading of {who} failed: {job.Error ?? "unknown error"}",
                JobStatus.Canceled => $"Grading of {who} was canceled",
                _ => $"Grading of {who}: still {job.Status.ToString().ToLowerInvariant()}",
            };
        }

        if (job.IsGroup)
        {
            var label = job.Type == JobType.GradeAssignment ? "Grade all" : "Grade student";
            var progress = job.ProgressTotal is { } total ? $", {job.ProgressDone ?? 0} of {total} gradings finished" : "";
            var counts = job is { IsFinished: true, Result: { } result }
                ? JobPayloads.Read<GradeAssignmentResult>(result) is var r
                    ? $": {r.Succeeded} drafted, {r.Failed} failed, {r.Canceled} canceled"
                    : ""
                : "";
            return $"{label} {job.Status.ToString().ToLowerInvariant()}{progress}{counts}";
        }

        var error = job.Status == JobStatus.Failed ? $": {job.Error ?? "unknown error"}" : "";
        return $"{job.Type} job {job.Status.ToString().ToLowerInvariant()}{error}";
    }

    private async Task<string> ScoreAsync(Job job, CancellationToken cancellationToken)
    {
        if (job.Result is null)
        {
            return "no score";
        }

        var grading = await _gradings.FindAsync(JobPayloads.Read<GradeVersionResult>(job.Result).GradingId, cancellationToken);
        return grading?.Score is { } score
            ? $"score {Points(score)} of {Points(grading.MaxScore)}"
            : "no score";
    }

    private static string Points(double? points)
    {
        return points?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
    }
}
