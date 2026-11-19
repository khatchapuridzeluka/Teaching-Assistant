namespace TeachingAssistant.Domain.Grading;

public enum GradingStatus
{
    Draft,

    Discarded,
}

public class AiGrading
{
    public const int MaxDiscardNoteLength = 500;

    private readonly List<GradingMistake> _mistakes = [];
    private readonly List<GradingStrength> _strengths = [];

    private AiGrading()
    {
    }

    public AiGrading(
        Guid versionId,
        string model,
        string review,
        double? score,
        double? maxScore,
        double? bonus,
        int instructionsVersion,
        IEnumerable<string> sentFiles,
        IEnumerable<string> skippedFiles,
        string rawOutput,
        IEnumerable<GradingMistake> mistakes,
        DateTimeOffset createdAt,
        IEnumerable<GradingStrength>? strengths = null)
    {
        Id = Guid.CreateVersion7();
        VersionId = versionId;
        Status = GradingStatus.Draft;
        Model = model;
        Review = review;
        Score = score;
        MaxScore = maxScore;
        Bonus = bonus;
        InstructionsVersion = instructionsVersion;
        SentFiles = sentFiles.ToArray();
        SkippedFiles = skippedFiles.ToArray();
        RawOutput = rawOutput;
        CreatedAt = createdAt.ToUniversalTime();

        var position = 0;
        foreach (var mistake in mistakes)
        {
            mistake.AttachTo(Id, position++);
            _mistakes.Add(mistake);
        }

        position = 0;
        foreach (var strength in strengths ?? [])
        {
            strength.AttachTo(Id, position++);
            _strengths.Add(strength);
        }
    }

    public Guid Id { get; private set; }

    public Guid VersionId { get; private set; }

    public GradingStatus Status { get; private set; }
    public string Model { get; private set; } = null!;

    public string Review { get; private set; } = null!;

    public double? Score { get; private set; }

    public double? MaxScore { get; private set; }

    public double? Bonus { get; private set; }

    public int InstructionsVersion { get; private set; }

    public string[] SentFiles { get; private set; } = [];
    public string[] SkippedFiles { get; private set; } = [];

    public string RawOutput { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DiscardedAt { get; private set; }

    /// <summary>Why it was discarded; null for drafts discarded before reasons were recorded.</summary>
    public DiscardReason? DiscardReason { get; private set; }

    /// <summary>The teacher's own words on why, when given.</summary>
    public string? DiscardNote { get; private set; }

    /// <summary>What compiling the code before grading found; null for gradings made before the check existed.</summary>
    public BuildStatus? BuildStatus { get; private set; }

    /// <summary>The compiler's messages, or why the check did not run.</summary>
    public string? BuildOutput { get; private set; }

    /// <summary>How many Checkstyle findings the check found; null when Checkstyle did not run.</summary>
    public int? StyleFindings { get; private set; }

    /// <summary>Checkstyle's findings, one per line, or why it did not finish.</summary>
    public string? StyleOutput { get; private set; }

    /// <summary>How many PMD findings the check found; null when PMD did not run.</summary>
    public int? PmdFindings { get; private set; }

    /// <summary>PMD's findings, one per line, or why it did not finish.</summary>
    public string? PmdOutput { get; private set; }

    public IReadOnlyList<GradingMistake> Mistakes => _mistakes;

    public IReadOnlyList<GradingStrength> Strengths => _strengths;

    public void RecordBuild(
        BuildStatus status, string output, int? styleFindings = null, string? styleOutput = null, int? pmdFindings = null,
        string? pmdOutput = null)
    {
        BuildStatus = status;
        BuildOutput = output;
        StyleFindings = styleFindings;
        StyleOutput = string.IsNullOrEmpty(styleOutput) ? null : styleOutput;
        PmdFindings = pmdFindings;
        PmdOutput = string.IsNullOrEmpty(pmdOutput) ? null : pmdOutput;
    }

    public void Discard(DateTimeOffset at, DiscardReason? reason = null, string? note = null)
    {
        if (Status == GradingStatus.Discarded)
        {
            return;
        }

        Status = GradingStatus.Discarded;
        DiscardedAt = at.ToUniversalTime();
        DiscardReason = reason;
        DiscardNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

public class GradingMistake
{
    private GradingMistake()
    {
    }

    public GradingMistake(
        string? file, int? startLine, int? endLine, MistakeSeverity severity, MistakeCategory category, string message,
        double? points = null)
    {
        Id = Guid.CreateVersion7();
        File = file;
        StartLine = startLine;
        EndLine = endLine;
        Severity = severity;
        Category = category;
        Message = message;
        Points = points;
    }

    public Guid Id { get; private set; }
    public Guid GradingId { get; private set; }

    public int Position { get; private set; }

    public string? File { get; private set; }

    public int? StartLine { get; private set; }
    public int? EndLine { get; private set; }
    public MistakeSeverity Severity { get; private set; }
    public MistakeCategory Category { get; private set; }
    public string Message { get; private set; } = null!;

    public double? Points { get; private set; }

    internal void AttachTo(Guid gradingId, int position)
    {
        GradingId = gradingId;
        Position = position;
    }
}
