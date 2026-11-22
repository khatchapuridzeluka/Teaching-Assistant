namespace TeachingAssistant.Domain.Grading;

public class MistakeCluster
{
    private readonly List<ClusterMember> _members = [];

    private MistakeCluster()
    {
    }

    public MistakeCluster(string title, string explanation, bool ungrouped, IEnumerable<ClusterMember> members)
    {
        Id = Guid.CreateVersion7();
        Title = title.Trim();
        Explanation = explanation.Trim();
        Ungrouped = ungrouped;
        foreach (var member in members)
        {
            member.AttachTo(Id);
            _members.Add(member);
        }

        if (_members.Count == 0)
        {
            throw new ArgumentException("A group needs at least one mistake.", nameof(members));
        }
    }

    public Guid Id { get; private set; }

    public Guid AnalysisId { get; private set; }

    public int Position { get; private set; }

    public string Title { get; private set; } = null!;

    public string Explanation { get; private set; } = null!;

    public bool Ungrouped { get; private set; }

    public string? ApprovedExplanation { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public IReadOnlyList<ClusterMember> Members => _members;

    public void Approve(string explanation, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(explanation))
        {
            throw new ArgumentException("The explanation cannot be empty.", nameof(explanation));
        }

        ApprovedExplanation = explanation.Trim();
        ApprovedAt = at.ToUniversalTime();
    }

    internal void AttachTo(Guid analysisId, int position)
    {
        AnalysisId = analysisId;
        Position = position;
    }
}
