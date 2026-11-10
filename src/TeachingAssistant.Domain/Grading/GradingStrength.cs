namespace TeachingAssistant.Domain.Grading;

public class GradingStrength
{
    private GradingStrength()
    {
    }

    public GradingStrength(string? file, int? startLine, int? endLine, MistakeCategory category, string message)
    {
        Id = Guid.CreateVersion7();
        File = file;
        StartLine = startLine;
        EndLine = endLine;
        Category = category;
        Message = message;
    }

    public Guid Id { get; private set; }

    public Guid GradingId { get; private set; }

    public int Position { get; private set; }

    public string? File { get; private set; }

    public int? StartLine { get; private set; }

    public int? EndLine { get; private set; }

    public MistakeCategory Category { get; private set; }

    public string Message { get; private set; } = null!;

    internal void AttachTo(Guid gradingId, int position)
    {
        GradingId = gradingId;
        Position = position;
    }
}

public sealed record ReportStrengthEdit(Guid SourceStrengthId, bool Rejected, MistakeCategory Category, string Message);

public class ReportStrength
{
    private ReportStrength()
    {
    }

    internal ReportStrength(Guid reportId, GradingStrength source)
    {
        Id = Guid.CreateVersion7();
        ReportId = reportId;
        SourceStrengthId = source.Id;
        Position = source.Position;
        File = source.File;
        StartLine = source.StartLine;
        EndLine = source.EndLine;
        Category = source.Category;
        Message = source.Message;
        Decision = MistakeDecision.Kept;
    }

    public Guid Id { get; private set; }

    public Guid ReportId { get; private set; }

    public Guid? SourceStrengthId { get; private set; }

    public int Position { get; private set; }

    public string? File { get; private set; }

    public int? StartLine { get; private set; }

    public int? EndLine { get; private set; }

    public MistakeCategory Category { get; private set; }

    public string Message { get; private set; } = null!;

    public MistakeDecision Decision { get; private set; }

    internal void Apply(ReportStrengthEdit edit, GradingStrength source)
    {
        Category = edit.Category;
        Message = edit.Message.Trim();

        if (edit.Rejected)
        {
            Decision = MistakeDecision.Rejected;
            return;
        }

        var changed = Category != source.Category || Message != source.Message.Trim();
        Decision = changed ? MistakeDecision.Edited : MistakeDecision.Kept;
    }
}
