namespace TeachingAssistant.Domain.Grading;

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
