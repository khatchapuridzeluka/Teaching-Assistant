namespace TeachingAssistant.Domain.Grading;

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
