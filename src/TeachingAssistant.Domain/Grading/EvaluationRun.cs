namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// One evaluation run: the approved reports of one course (or all) graded again on the side by one or more models, to compare
/// the models on the same homework against the teacher's final word. Nothing it produces is a draft: it never replaces,
/// discards or adds to a student's gradings.
/// </summary>
public class EvaluationRun
{
    private EvaluationRun()
    {
    }

    public EvaluationRun(string? courseId, IEnumerable<string> models, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CourseId = courseId;
        Models = models.ToArray();
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    /// <summary>The course whose approved reports were graded again; null for all courses.</summary>
    public string? CourseId { get; private set; }

    public string[] Models { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }
}

public enum RunResultStatus
{
    Pending,

    Done,

    Failed,
}

/// <summary>A mistake as a model found it in an evaluation run.</summary>
public sealed record RunMistake(
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points);

/// <summary>One approved report graded again by one model in an evaluation run.</summary>
public class EvaluationRunResult
{
    public const int MaxErrorLength = 2000;

    private EvaluationRunResult()
    {
    }

    public EvaluationRunResult(Guid runId, Guid reportId, Guid versionId, string model, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        RunId = runId;
        ReportId = reportId;
        VersionId = versionId;
        Model = model;
        Status = RunResultStatus.Pending;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    /// <summary>The approved report this result is compared with.</summary>
    public Guid ReportId { get; private set; }

    /// <summary>The submission version that was graded, the same one the report is about.</summary>
    public Guid VersionId { get; private set; }

    /// <summary>The model asked to grade.</summary>
    public string Model { get; private set; } = null!;

    /// <summary>The model that answered (a fallback model when the requested one declined).</summary>
    public string? AnsweredModel { get; private set; }

    public RunResultStatus Status { get; private set; }

    public double? Score { get; private set; }

    public double? MaxScore { get; private set; }

    public double? Bonus { get; private set; }

    public List<RunMistake> Mistakes { get; private set; } = [];

    public BuildStatus? BuildStatus { get; private set; }

    public string? Error { get; private set; }

    public int? DurationMs { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(
        string answeredModel, double? score, double? maxScore, double? bonus, IEnumerable<RunMistake> mistakes, BuildStatus? buildStatus,
        TimeSpan duration, DateTimeOffset at)
    {
        AnsweredModel = answeredModel;
        Score = score;
        MaxScore = maxScore;
        Bonus = bonus;
        Mistakes = mistakes.ToList();
        BuildStatus = buildStatus;
        DurationMs = (int)Math.Round(duration.TotalMilliseconds);
        Error = null;
        Status = RunResultStatus.Done;
        CompletedAt = at.ToUniversalTime();
    }

    public void Fail(string error, DateTimeOffset at)
    {
        Error = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        Status = RunResultStatus.Failed;
        CompletedAt = at.ToUniversalTime();
    }
}
