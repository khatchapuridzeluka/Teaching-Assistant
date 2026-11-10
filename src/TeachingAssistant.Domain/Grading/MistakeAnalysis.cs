namespace TeachingAssistant.Domain.Grading;

public class MistakeAnalysis
{
    private readonly List<MistakeCluster> _clusters = [];

    private MistakeAnalysis()
    {
    }

    public MistakeAnalysis(
        string courseId, string assignmentId, string model, string summary, string nextLecture, int students, int mistakes,
        IEnumerable<MistakeCluster> clusters, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CourseId = courseId;
        AssignmentId = assignmentId;
        Model = model;
        Summary = summary.Trim();
        NextLecture = nextLecture.Trim();
        Students = students;
        Mistakes = mistakes;
        CreatedAt = createdAt.ToUniversalTime();

        var position = 0;
        foreach (var cluster in clusters)
        {
            cluster.AttachTo(Id, position++);
            _clusters.Add(cluster);
        }
    }

    public Guid Id { get; private set; }

    public string CourseId { get; private set; } = null!;

    public string AssignmentId { get; private set; } = null!;

    public string Model { get; private set; } = null!;

    public string Summary { get; private set; } = "";

    public string NextLecture { get; private set; } = "";

    public int Students { get; private set; }

    public int Mistakes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<MistakeCluster> Clusters => _clusters;
}

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

public class ClusterMember
{
    private ClusterMember()
    {
    }

    public ClusterMember(
        Guid gradingMistakeId, Guid versionId, string studentId, string studentName, string submissionId, MistakeSeverity severity,
        MistakeCategory category, double? points, string message, string? file, int? startLine, int? endLine)
    {
        Id = Guid.CreateVersion7();
        GradingMistakeId = gradingMistakeId;
        VersionId = versionId;
        StudentId = studentId;
        StudentName = studentName;
        SubmissionId = submissionId;
        Severity = severity;
        Category = category;
        Points = points;
        Message = message;
        File = file;
        StartLine = startLine;
        EndLine = endLine;
    }

    public Guid Id { get; private set; }

    public Guid ClusterId { get; private set; }

    public Guid GradingMistakeId { get; private set; }

    public Guid VersionId { get; private set; }

    public string StudentId { get; private set; } = null!;

    public string StudentName { get; private set; } = null!;

    public string SubmissionId { get; private set; } = null!;

    public MistakeSeverity Severity { get; private set; }

    public MistakeCategory Category { get; private set; }

    public double? Points { get; private set; }

    public string Message { get; private set; } = null!;

    public string? File { get; private set; }

    public int? StartLine { get; private set; }

    public int? EndLine { get; private set; }

    internal void AttachTo(Guid clusterId)
    {
        ClusterId = clusterId;
    }
}
