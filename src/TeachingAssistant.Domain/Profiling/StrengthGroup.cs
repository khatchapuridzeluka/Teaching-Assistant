namespace TeachingAssistant.Domain.Profiling;

public class StrengthGroup
{
    private StrengthGroup()
    {
    }

    public StrengthGroup(string title, string explanation, bool ungrouped, IEnumerable<Guid> strengthIds)
    {
        Id = Guid.CreateVersion7();
        Title = title.Trim();
        Explanation = explanation.Trim();
        Ungrouped = ungrouped;
        StrengthIds = strengthIds.ToArray();
    }

    public Guid Id { get; private set; }

    public Guid AnalysisId { get; private set; }

    public int Position { get; private set; }

    public string Title { get; private set; } = null!;

    public string Explanation { get; private set; } = "";

    public bool Ungrouped { get; private set; }

    public Guid[] StrengthIds { get; private set; } = [];

    internal void AttachTo(Guid analysisId, int position)
    {
        AnalysisId = analysisId;
        Position = position;
    }
}
