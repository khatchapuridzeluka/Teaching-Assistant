using System.Globalization;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class JobReporter
{
    private readonly IJobStore _jobs;
    private readonly IGradingStore _gradings;
    private readonly IMistakeAnalysisStore _analyses;
    private readonly IStudentAnalysisService _studentAnalyses;

    public JobReporter(IJobStore jobs, IGradingStore gradings, IMistakeAnalysisStore analyses, IStudentAnalysisService studentAnalyses)
    {
        _jobs = jobs;
        _gradings = gradings;
        _analyses = analyses;
        _studentAnalyses = studentAnalyses;
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

    public async Task<IReadOnlyList<AgentLink>> LinksAsync(IReadOnlyList<Job> jobs, CancellationToken cancellationToken = default)
    {
        var finished = jobs.Where(j => j.Status == JobStatus.Succeeded).ToList();
        var versionIds = finished.Where(j => j.Type == JobType.GradeVersion)
            .Select(j => JobPayloads.Read<GradeVersionPayload>(j.Payload).VersionId)
            .ToList();
        var subjects = (await _jobs.GetSubjectsAsync(versionIds, cancellationToken)).ToDictionary(s => s.VersionId);

        var links = new List<AgentLink>();
        foreach (var job in finished)
        {
            if (job.Type == JobType.GradeVersion
                && subjects.GetValueOrDefault(JobPayloads.Read<GradeVersionPayload>(job.Payload).VersionId) is { } s)
            {
                links.Add(new AgentLink
                {
                    Kind = AgentLinkKind.Submission,
                    Label = $"{s.StudentName}'s draft · {s.AssignmentTitle ?? "assignment"}",
                    CourseId = s.CourseId,
                    AssignmentId = s.AssignmentId,
                    SubmissionId = s.SubmissionId,
                });
            }
            else if (job is { Type: JobType.GradeAssignment, CourseId: { } courseId, AssignmentId: { } assignmentId })
            {
                links.Add(new AgentLink
                {
                    Kind = AgentLinkKind.Assignment,
                    Label = "The assignment's drafts",
                    CourseId = courseId,
                    AssignmentId = assignmentId,
                });
            }
            else if (job is { Type: JobType.GradeStudent, CourseId: { } studentCourse })
            {
                var student = JobPayloads.Read<GradeStudentPayload>(job.Payload);
                links.Add(new AgentLink
                {
                    Kind = AgentLinkKind.Student,
                    Label = $"{student.StudentName ?? "The student"}'s profile",
                    CourseId = studentCourse,
                    StudentId = student.StudentId,
                });
            }
            else if (job is { Type: JobType.AnalyzeMistakes, CourseId: { } mistakesCourse, AssignmentId: { } mistakesAssignment })
            {
                links.Add(new AgentLink
                {
                    Kind = AgentLinkKind.CommonMistakes,
                    Label = "Common mistakes",
                    CourseId = mistakesCourse,
                    AssignmentId = mistakesAssignment,
                });
            }
            else if (job.Type == JobType.AnalyzeStudent)
            {
                var analyzed = JobPayloads.Read<AnalyzeStudentPayload>(job.Payload);
                var owner = analyzed.StudentName is { } name ? $"{name}'s" : "The student's";
                links.Add(StudentAnalysisTools.Link(analyzed.Kind, owner, analyzed.CourseId, analyzed.StudentId));
            }
        }

        return links;
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
            var label = job.Type switch
            {
                JobType.GradeAssignment => "Grade all",
                JobType.EvaluationRun => "Evaluation run",
                _ => "Grade student",
            };
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

        if (job.Type == JobType.AnalyzeStudent)
        {
            return await DescribeStudentAnalysisAsync(job, cancellationToken);
        }

        var error = job.Status == JobStatus.Failed ? $": {job.Error ?? "unknown error"}" : "";
        var name = job.Type switch
        {
            JobType.AnalyzeMistakes => "Common mistakes analysis",
            JobType.GenerateTests => "Test case generation",
            JobType.GenerateTestCode => "Test code generation",
            JobType.RunTests => "Test run",
            _ => $"{job.Type} job",
        };
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
            .Select(c => (c.Title, Students: c.Members.Select(m => m.StudentId).Distinct().Count(), Mistakes: c.Members.Count))
            .OrderByDescending(c => c.Students)
            .Select(c => $"  - {c.Title}: {Count(c.Students, "student")}, {Count(c.Mistakes, "mistake")}")
            .ToList();
        var ungrouped = analysis.Clusters.Where(c => c.Ungrouped).Sum(c => c.Members.Count);
        var rest = ungrouped > 0 ? $"\n  ({ungrouped} mistakes not grouped)" : "";
        return $"Common mistakes analysis ready: {analysis.Mistakes} mistakes from {analysis.Students} students.\n"
            + $"  Overview: {analysis.Summary}\n  Groups:\n{string.Join("\n", groups)}{rest}\n  For the next lecture: {analysis.NextLecture}";
    }

    private async Task<string> DescribeStudentAnalysisAsync(Job job, CancellationToken cancellationToken)
    {
        var analyzed = JobPayloads.Read<AnalyzeStudentPayload>(job.Payload);
        var name = $"{StudentAnalysisTools.Name(analyzed.Kind)} of {analyzed.StudentName ?? "the student"}";
        if (job.Status != JobStatus.Succeeded)
        {
            var error = job.Status == JobStatus.Failed ? $": {job.Error ?? "unknown error"}" : "";
            return $"The {name} {job.Status.ToString().ToLowerInvariant()}{error}";
        }

        var analysisId = job.Result is null ? (Guid?)null : JobPayloads.Read<AnalyzeStudentResult>(job.Result).AnalysisId;
        var analysis = await _studentAnalyses.GetLatestAsync(analyzed.CourseId, analyzed.StudentId, analyzed.Kind, cancellationToken);
        if (analysis is null || analysis.Id != analysisId)
        {
            return $"The {name} finished; open the student's page for the result";
        }

        return $"The {name} is ready.\n  {StudentAnalysisTools.Describe(analysis).Replace("\n", "\n  ")}";
    }

    private static string Count(int count, string noun)
    {
        return $"{count} {noun}{(count == 1 ? "" : "s")}";
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
