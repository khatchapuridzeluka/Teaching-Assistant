namespace TeachingAssistant.Domain.Testing;

public class TestRun
{
    private TestRun()
    {
    }

    public TestRun(
        Guid testSetId, int testSetVersion, Guid versionId, string courseId, string assignmentId, string studentId,
        TestRunStatus status, string? contract, string? adapter, string? adapterModel, string results, string? output,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        TestSetId = testSetId;
        TestSetVersion = testSetVersion;
        VersionId = versionId;
        CourseId = courseId;
        AssignmentId = assignmentId;
        StudentId = studentId;
        Status = status;
        Interface = contract;
        Adapter = adapter;
        AdapterModel = adapterModel;
        Results = results;
        Output = output;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid TestSetId { get; private set; }

    public int TestSetVersion { get; private set; }

    public Guid VersionId { get; private set; }

    public string CourseId { get; private set; } = null!;

    public string AssignmentId { get; private set; } = null!;

    public string StudentId { get; private set; } = null!;

    public TestRunStatus Status { get; private set; }

    public string? Interface { get; private set; }

    public string? Adapter { get; private set; }

    public string? AdapterModel { get; private set; }

    public string Results { get; private set; } = null!;

    public string? Output { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
