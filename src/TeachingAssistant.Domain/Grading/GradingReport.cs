using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Domain.Grading;

public enum ReportStatus
{
    InReview,

    Approved,
}

public enum MistakeDecision
{
    Kept,

    Edited,

    Rejected,
}

public sealed record ReportMistakeEdit(
    Guid SourceMistakeId,
    bool Rejected,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message);

public class GradingReport
{
    private readonly List<ReportMistake> _mistakes = [];

    private GradingReport()
    {
    }

    public GradingReport(AiGrading grading, Submission submission, DateTimeOffset createdAt)
    {
        if (grading.Status == GradingStatus.Discarded)
        {
            throw new InvalidOperationException($"Grading {grading.Id} is discarded and cannot be reviewed.");
        }

        if (submission.Versions.All(v => v.Id != grading.VersionId))
        {
            throw new ArgumentException($"Grading {grading.Id} is not about a version of submission {submission.Id}.", nameof(submission));
        }

        Id = Guid.CreateVersion7();
        GradingId = grading.Id;
        VersionId = grading.VersionId;
        StudentId = submission.StudentId;
        CourseId = submission.CourseId;
        AssignmentId = submission.AssignmentId;
        Status = ReportStatus.InReview;
        Score = grading.Score;
        Bonus = grading.Bonus;
        Review = grading.Review;
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = CreatedAt;

        foreach (var mistake in grading.Mistakes.OrderBy(m => m.Position))
        {
            _mistakes.Add(new ReportMistake(Id, mistake));
        }
    }

    public Guid Id { get; private set; }

    public Guid GradingId { get; private set; }

    public Guid VersionId { get; private set; }

    public string StudentId { get; private set; } = null!;

    public string CourseId { get; private set; } = null!;

    public string AssignmentId { get; private set; } = null!;

    public ReportStatus Status { get; private set; }

    public double? Score { get; private set; }

    public double? Bonus { get; private set; }

    public string Review { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public IReadOnlyList<ReportMistake> Mistakes => _mistakes;

    public void Save(
        AiGrading grading, double? score, double? bonus, string review, IEnumerable<ReportMistakeEdit> edits, DateTimeOffset at)
    {
        if (grading.Id != GradingId)
        {
            throw new ArgumentException($"Report {Id} belongs to grading {GradingId}, not {grading.Id}.", nameof(grading));
        }

        if (score < 0 || bonus < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score and bonus cannot be negative.");
        }

        foreach (var edit in edits)
        {
            var mistake = _mistakes.SingleOrDefault(m => m.SourceMistakeId == edit.SourceMistakeId)
                          ?? throw new ArgumentException($"Mistake {edit.SourceMistakeId} is not part of this report.", nameof(edits));
            var source = grading.Mistakes.Single(m => m.Id == edit.SourceMistakeId);
            mistake.Apply(edit, source);
        }

        Score = score;
        Bonus = bonus;
        Review = review;
        Status = ReportStatus.InReview;
        ApprovedAt = null;
        UpdatedAt = at.ToUniversalTime();
    }

    public void Approve(DateTimeOffset at)
    {
        Status = ReportStatus.Approved;
        ApprovedAt = at.ToUniversalTime();
        UpdatedAt = ApprovedAt.Value;
    }
}

public class ReportMistake
{
    private ReportMistake()
    {
    }

    internal ReportMistake(Guid reportId, GradingMistake source)
    {
        Id = Guid.CreateVersion7();
        ReportId = reportId;
        SourceMistakeId = source.Id;
        Position = source.Position;
        File = source.File;
        StartLine = source.StartLine;
        EndLine = source.EndLine;
        Severity = source.Severity;
        Category = source.Category;
        Message = source.Message;
        Decision = MistakeDecision.Kept;
    }

    public Guid Id { get; private set; }

    public Guid ReportId { get; private set; }

    public Guid? SourceMistakeId { get; private set; }

    public int Position { get; private set; }

    public string? File { get; private set; }

    public int? StartLine { get; private set; }

    public int? EndLine { get; private set; }

    public MistakeSeverity Severity { get; private set; }

    public MistakeCategory Category { get; private set; }

    public string Message { get; private set; } = null!;

    public MistakeDecision Decision { get; private set; }

    internal void Apply(ReportMistakeEdit edit, GradingMistake source)
    {
        Severity = edit.Severity;
        Category = edit.Category;
        Message = edit.Message.Trim();

        if (edit.Rejected)
        {
            Decision = MistakeDecision.Rejected;
            return;
        }

        var changed = Severity != source.Severity || Category != source.Category || Message != source.Message.Trim();
        Decision = changed ? MistakeDecision.Edited : MistakeDecision.Kept;
    }
}
