namespace TeachingAssistant.Domain.Grading;

public enum GradingStatus
{
    Draft,

    Discarded,
}

public class AiGrading
{
    private readonly List<GradingMistake> _mistakes = [];

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

    public IReadOnlyList<GradingMistake> Mistakes => _mistakes;

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

public class GradingMistake
{
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

    public int Position { get; private set; }

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
