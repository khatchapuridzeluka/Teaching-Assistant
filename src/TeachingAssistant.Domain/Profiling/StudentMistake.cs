using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Domain.Profiling;

public class StudentMistake
{
    private StudentMistake()
    {
    }

    private StudentMistake(AiGrading grading, GradingMistake mistake, Submission submission)
    {
        Id = Guid.CreateVersion7();
        StudentId = submission.StudentId;
        CourseId = submission.CourseId;
        AssignmentId = submission.AssignmentId;
        SubmissionId = submission.Id;
        VersionId = grading.VersionId;
        GradingId = grading.Id;
        SourceMistakeId = mistake.Id;
        File = mistake.File;
        StartLine = mistake.StartLine;
        EndLine = mistake.EndLine;
        Severity = mistake.Severity;
        Category = mistake.Category;
        Message = mistake.Message;
        Points = mistake.Points;
        Decision = MistakeDecision.Kept;
        ApprovedAt = grading.CreatedAt;
    }

    private StudentMistake(GradingReport report, ReportMistake mistake, Submission submission, DateTimeOffset at)
    {
        Id = Guid.CreateVersion7();
        StudentId = submission.StudentId;
        CourseId = submission.CourseId;
        AssignmentId = submission.AssignmentId;
        SubmissionId = submission.Id;
        VersionId = report.VersionId;
        GradingId = report.GradingId;
        ReportId = report.Id;
        ReportMistakeId = mistake.Id;
        SourceMistakeId = mistake.SourceMistakeId;
        File = mistake.File;
        StartLine = mistake.StartLine;
        EndLine = mistake.EndLine;
        Severity = mistake.Severity;
        Category = mistake.Category;
        Message = mistake.Message;
        Points = mistake.Points;
        Decision = mistake.Decision;
        ApprovedAt = at;
    }

    public Guid Id { get; private set; }

    public string StudentId { get; private set; } = null!;

    public string CourseId { get; private set; } = null!;

    public string AssignmentId { get; private set; } = null!;

    public Guid SubmissionId { get; private set; }

    public Guid VersionId { get; private set; }

    public Guid GradingId { get; private set; }

    public Guid ReportId { get; private set; }

    public Guid ReportMistakeId { get; private set; }

    public Guid? SourceMistakeId { get; private set; }

    public string? File { get; private set; }

    public int? StartLine { get; private set; }

    public int? EndLine { get; private set; }

    public MistakeSeverity Severity { get; private set; }

    public MistakeCategory Category { get; private set; }

    public string Message { get; private set; } = null!;

    public double? Points { get; private set; }

    public MistakeDecision Decision { get; private set; }

    public DateTimeOffset ApprovedAt { get; private set; }

    public static IReadOnlyList<StudentMistake> FromApprovedReport(GradingReport report, Submission submission)
    {
        if (report.Status != ReportStatus.Approved)
        {
            throw new InvalidOperationException($"Report {report.Id} is not approved; only approved mistakes are recorded.");
        }

        return FromReport(report, submission);
    }

    public static IReadOnlyList<StudentMistake> FromReport(GradingReport report, Submission submission)
    {
        if (submission.Versions.All(v => v.Id != report.VersionId))
        {
            throw new ArgumentException($"Report {report.Id} is not about a version of submission {submission.Id}.", nameof(submission));
        }

        var at = report.ApprovedAt ?? report.UpdatedAt;
        return report.Mistakes
            .Where(m => m.Decision != MistakeDecision.Rejected)
            .OrderBy(m => m.Position)
            .Select(m => new StudentMistake(report, m, submission, at))
            .ToList();
    }

    public static IReadOnlyList<StudentMistake> FromDraft(AiGrading grading, Submission submission)
    {
        if (grading.Status != GradingStatus.Draft)
        {
            throw new InvalidOperationException($"Grading {grading.Id} was discarded; only current drafts describe a student.");
        }

        if (submission.Versions.All(v => v.Id != grading.VersionId))
        {
            throw new ArgumentException($"Grading {grading.Id} is not about a version of submission {submission.Id}.", nameof(submission));
        }

        return grading.Mistakes
            .OrderBy(m => m.Position)
            .Select(m => new StudentMistake(grading, m, submission))
            .ToList();
    }
}
