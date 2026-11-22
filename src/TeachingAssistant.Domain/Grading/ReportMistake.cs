namespace TeachingAssistant.Domain.Grading;

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
        Points = source.Points;
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

    public double? Points { get; private set; }

    public MistakeDecision Decision { get; private set; }

    internal void Apply(ReportMistakeEdit edit, GradingMistake source)
    {
        Severity = edit.Severity;
        Category = edit.Category;
        Message = edit.Message.Trim();
        Points = edit.Points;

        if (edit.Rejected)
        {
            Decision = MistakeDecision.Rejected;
            return;
        }

        var changed = Severity != source.Severity || Category != source.Category || Message != source.Message.Trim()
                      || Points != source.Points;
        Decision = changed ? MistakeDecision.Edited : MistakeDecision.Kept;
    }
}
