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
