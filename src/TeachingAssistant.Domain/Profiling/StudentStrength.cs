using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Domain.Profiling;

public class StudentStrength
{
    private StudentStrength()
    {
    }

    private StudentStrength(GradingReport report, ReportStrength strength, Submission submission)
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
        ApprovedAt = report.ApprovedAt!.Value;
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

        if (submission.Versions.All(v => v.Id != report.VersionId))
        {
            throw new ArgumentException($"Report {report.Id} is not about a version of submission {submission.Id}.", nameof(submission));
        }

        return report.Strengths
            .Where(s => s.Decision != MistakeDecision.Rejected)
            .OrderBy(s => s.Position)
            .Select(s => new StudentStrength(report, s, submission))
            .ToList();
    }
}
