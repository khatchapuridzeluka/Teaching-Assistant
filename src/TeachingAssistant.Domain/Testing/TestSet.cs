namespace TeachingAssistant.Domain.Testing;

public class TestSet
{
    private TestSet()
    {
    }

    public TestSet(
        string courseId, string assignmentId, int version, TestStyle style, string request, string content, string model,
        DateTimeOffset createdAt)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "A test set's version starts at 1.");
        }

        Id = Guid.CreateVersion7();
        CourseId = courseId;
        AssignmentId = assignmentId;
        Version = version;
        Style = style;
        Status = TestSetStatus.Draft;
        Request = request;
        Content = content;
        Model = model;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public string CourseId { get; private set; } = null!;

    public string AssignmentId { get; private set; } = null!;

    public int Version { get; private set; }

    public TestStyle Style { get; private set; }

    public TestSetStatus Status { get; private set; }

    public string Request { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public string Model { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public void Edit(string content)
    {
        if (Status != TestSetStatus.Draft)
        {
            throw new InvalidOperationException("An approved test set is not edited in place; editing it makes a new version.");
        }

        Content = content;
    }

    public void Approve()
    {
        Status = TestSetStatus.Approved;
    }
}
