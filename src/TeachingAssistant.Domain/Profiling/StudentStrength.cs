using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Domain.Profiling;

public class StudentStrength
{
    private StudentStrength()
    {
    }

    private StudentStrength(AiGrading grading, GradingStrength strength, Submission submission)
    {
        Id = Guid.CreateVersion7();
        StudentId = submission.StudentId;
        CourseId = submission.CourseId;
        AssignmentId = submission.AssignmentId;
        SubmissionId = submission.Id;
        VersionId = grading.VersionId;
        GradingId = grading.Id;
        SourceStrengthId = strength.Id;
        File = strength.File;
        StartLine = strength.StartLine;
        EndLine = strength.EndLine;
        Category = strength.Category;
        Message = strength.Message;
        Decision = MistakeDecision.Kept;
        ApprovedAt = grading.CreatedAt;
    }

    private StudentStrength(GradingReport report, ReportStrength strength, Submission submission, DateTimeOffset at)
    {
        Id = Guid.CreateVersion7();
        StudentId = submission.StudentId;
        CourseId = submission.CourseId;
        AssignmentId = submission.AssignmentId;
        SubmissionId = submission.Id;
        VersionId = report.VersionId;
        GradingId = report.GradingId;
        ReportId = report.Id;
        ReportStrengthId = strength.Id;
        SourceStrengthId = strength.SourceStrengthId;
        File = strength.File;
        StartLine = strength.StartLine;
        EndLine = strength.EndLine;
        Category = strength.Category;
        Message = strength.Message;
        Decision = strength.Decision;
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

    public Guid ReportStrengthId { get; private set; }

    public Guid? SourceStrengthId { get; private set; }

    public string? File { get; private set; }

    public int? StartLine { get; private set; }

    public int? EndLine { get; private set; }

    public MistakeCategory Category { get; private set; }

    public string Message { get; private set; } = null!;

    public MistakeDecision Decision { get; private set; }

    public DateTimeOffset ApprovedAt { get; private set; }

    public static IReadOnlyList<StudentStrength> FromApprovedReport(GradingReport report, Submission submission)
    {
        if (report.Status != ReportStatus.Approved)
        {
            throw new InvalidOperationException($"Report {report.Id} is not approved; only approved strengths are recorded.");
        }

        return FromReport(report, submission);
    }

    public static IReadOnlyList<StudentStrength> FromReport(GradingReport report, Submission submission)
    {
        if (submission.Versions.All(v => v.Id != report.VersionId))
        {
            throw new ArgumentException($"Report {report.Id} is not about a version of submission {submission.Id}.", nameof(submission));
        }

        var at = report.ApprovedAt ?? report.UpdatedAt;
        return report.Strengths
            .Where(s => s.Decision != MistakeDecision.Rejected)
            .OrderBy(s => s.Position)
            .Select(s => new StudentStrength(report, s, submission, at))
            .ToList();
    }

    public static IReadOnlyList<StudentStrength> FromDraft(AiGrading grading, Submission submission)
    {
        if (grading.Status != GradingStatus.Draft)
        {
            throw new InvalidOperationException($"Grading {grading.Id} was discarded; only current drafts describe a student.");
        }

        if (submission.Versions.All(v => v.Id != grading.VersionId))
        {
            throw new ArgumentException($"Grading {grading.Id} is not about a version of submission {submission.Id}.", nameof(submission));
        }

        return grading.Strengths
            .OrderBy(s => s.Position)
            .Select(s => new StudentStrength(grading, s, submission))
            .ToList();
    }
}
