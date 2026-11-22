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
