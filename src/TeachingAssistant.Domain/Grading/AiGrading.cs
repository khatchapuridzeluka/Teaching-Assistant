namespace TeachingAssistant.Domain.Grading;

/// <summary>Where a grading stands. Approved (the teacher's edited feedback) comes with editing.</summary>
public enum GradingStatus
{
    /// <summary>The model's review as it answered; waiting for the teacher.</summary>
    Draft,

    /// <summary>A run the teacher threw away; kept in the history.</summary>
    Discarded,
}

/// <summary>
/// One AI grading of one stored version: the model's whole answer (review text, score, mistakes) saved as a draft the
/// moment it arrives, so a paid grading is never lost and opening it again costs nothing. Grading again adds a new one;
/// the newest draft of a version is its current one. The model's original is never changed: the teacher's edits will be
/// stored next to it. Never written to Classroom.
/// </summary>
public class AiGrading
{
    private readonly List<GradingMistake> _mistakes = [];

    // For EF Core.
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
        DateTimeOffset createdAt)
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
    }

    public Guid Id { get; private set; }

    /// <summary>The stored version (snapshot of a student's work) that was graded.</summary>
    public Guid VersionId { get; private set; }

    public GradingStatus Status { get; private set; }
    public string Model { get; private set; } = null!;

    /// <summary>Markdown: the score per task and short feedback.</summary>
    public string Review { get; private set; } = null!;

    /// <summary>Total points awarded, without the bonus; null when the model gave none.</summary>
    public double? Score { get; private set; }

    public double? MaxScore { get; private set; }

    /// <summary>Bonus points awarded, kept apart from the score; null when there is no bonus.</summary>
    public double? Bonus { get; private set; }

    /// <summary>The stored instructions version it was graded against.</summary>
    public int InstructionsVersion { get; private set; }

    public string[] SentFiles { get; private set; } = [];
    public string[] SkippedFiles { get; private set; } = [];

    /// <summary>The model's answer exactly as received, kept in case reading it ever needs to be redone.</summary>
    public string RawOutput { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DiscardedAt { get; private set; }

    /// <summary>In the order the model listed them.</summary>
    public IReadOnlyList<GradingMistake> Mistakes => _mistakes;

    /// <summary>Throws the draft away; it stays in the history. Discarding twice changes nothing.</summary>
    public void Discard(DateTimeOffset at)
    {
        if (Status == GradingStatus.Discarded)
        {
            return;
        }

        Status = GradingStatus.Discarded;
        DiscardedAt = at.ToUniversalTime();
    }
}

/// <summary>One mistake of a grading, pointing at lines of the graded code where it can (checked when it was saved).</summary>
public class GradingMistake
{
    // For EF Core.
    private GradingMistake()
    {
    }

    public GradingMistake(string? file, int? startLine, int? endLine, MistakeSeverity severity, MistakeCategory category, string message)
    {
        Id = Guid.CreateVersion7();
        File = file;
        StartLine = startLine;
        EndLine = endLine;
        Severity = severity;
        Category = category;
        Message = message;
    }

    public Guid Id { get; private set; }
    public Guid GradingId { get; private set; }

    /// <summary>The order the model listed it in, from 0.</summary>
    public int Position { get; private set; }

    /// <summary>A path from the version's code; null for a mistake about the whole submission.</summary>
    public string? File { get; private set; }

    public int? StartLine { get; private set; }
    public int? EndLine { get; private set; }
    public MistakeSeverity Severity { get; private set; }
    public MistakeCategory Category { get; private set; }
    public string Message { get; private set; } = null!;

    internal void AttachTo(Guid gradingId, int position)
    {
        GradingId = gradingId;
        Position = position;
    }
}
