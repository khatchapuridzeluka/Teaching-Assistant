using System.Globalization;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class JobReporter
{
    private readonly IJobStore _jobs;
    private readonly IGradingStore _gradings;
    private readonly IMistakeAnalysisStore _analyses;

    public JobReporter(IJobStore jobs, IGradingStore gradings, IMistakeAnalysisStore analyses)
    {
        _jobs = jobs;
        _gradings = gradings;
        _analyses = analyses;
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

        if (job is { Type: JobType.AnalyzeMistakes, Status: JobStatus.Succeeded })
        {
            return await DescribeAnalysisAsync(job, cancellationToken);
        }

        var error = job.Status == JobStatus.Failed ? $": {job.Error ?? "unknown error"}" : "";
        var name = job.Type == JobType.AnalyzeMistakes ? "Common mistakes analysis" : $"{job.Type} job";
        return $"{name} {job.Status.ToString().ToLowerInvariant()}{error}";
    }

    private async Task<string> DescribeAnalysisAsync(Job job, CancellationToken cancellationToken)
    {
        var analysisId = job.Result is null ? (Guid?)null : JobPayloads.Read<AnalyzeMistakesResult>(job.Result).AnalysisId;
        var analysis = job is { CourseId: { } courseId, AssignmentId: { } assignmentId }
            ? await _analyses.GetLatestAsync(courseId, assignmentId, cancellationToken)
            : null;
        if (analysis is null || analysis.Id != analysisId)
        {
            return "Common mistakes analysis finished; open the assignment's Common mistakes page for the result";
        }

        var groups = analysis.Clusters
            .Where(c => !c.Ungrouped)
            .OrderByDescending(c => c.Members.Select(m => m.StudentId).Distinct().Count())
            .Select(c => $"  - {c.Title}: {c.Members.Select(m => m.StudentId).Distinct().Count()} students, {c.Members.Count} mistakes")
            .ToList();
        var ungrouped = analysis.Clusters.Where(c => c.Ungrouped).Sum(c => c.Members.Count);
        var rest = ungrouped > 0 ? $"\n  ({ungrouped} mistakes not grouped)" : "";
        return $"Common mistakes analysis ready: {analysis.Mistakes} mistakes from {analysis.Students} students.\n"
            + $"  Overview: {analysis.Summary}\n  Groups:\n{string.Join("\n", groups)}{rest}\n  For the next lecture: {analysis.NextLecture}";
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
