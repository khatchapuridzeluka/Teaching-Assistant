namespace TeachingAssistant.Domain.Profiling;

public enum StudentAnalysisKind
{
    Mistakes,

    Strengths,
}

public class StudentAnalysis
{
    private readonly List<StrengthGroup> _groups = [];

    private StudentAnalysis()
    {
    }

    public StudentAnalysis(
        string courseId, string studentId, StudentAnalysisKind kind, string model, string summary, string nextSteps, int assignments,
        int records, DateTimeOffset? recordsApprovedUntil, DateTimeOffset createdAt, IEnumerable<StrengthGroup>? groups = null)
    {
        Id = Guid.CreateVersion7();
        CourseId = courseId;
        StudentId = studentId;
        Kind = kind;
        Model = model;
        Summary = summary.Trim();
        NextSteps = nextSteps.Trim();
        Assignments = assignments;
        Records = records;
        RecordsApprovedUntil = recordsApprovedUntil?.ToUniversalTime();
        CreatedAt = createdAt.ToUniversalTime();

        var position = 0;
        foreach (var group in groups ?? [])
        {
            group.AttachTo(Id, position++);
            _groups.Add(group);
        }
    }

    public Guid Id { get; private set; }

    public string CourseId { get; private set; } = null!;

    public string StudentId { get; private set; } = null!;

    public StudentAnalysisKind Kind { get; private set; }

    public string Model { get; private set; } = null!;

    public string Summary { get; private set; } = "";

    public string NextSteps { get; private set; } = "";

    public int Assignments { get; private set; }

    public int Records { get; private set; }

    public DateTimeOffset? RecordsApprovedUntil { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<StrengthGroup> Groups => _groups;

    public bool IsOutdated(int records, DateTimeOffset? recordsApprovedUntil)
    {
        return records != Records || recordsApprovedUntil?.ToUniversalTime() != RecordsApprovedUntil;
    }
}

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
