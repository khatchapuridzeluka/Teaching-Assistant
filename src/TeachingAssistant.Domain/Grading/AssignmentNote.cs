namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// The teacher's note for one assignment: something that differs for this task only (the point of the task, a different
/// rule, something to ignore). A short title for people and the exact instruction the grader gets. Deleting only marks it
/// deleted, so earlier gradings can still be explained.
/// </summary>
public class AssignmentNote
{
    public const int MaxTitleLength = 120;
    public const int MaxDetailLength = 2000;

    private AssignmentNote()
    {
    }

    public AssignmentNote(Guid id, string courseId, string assignmentId, int position, string title, string detail, DateTimeOffset createdAt)
    {
        Id = id;
        CourseId = courseId;
        AssignmentId = assignmentId;
        Position = position;
        Title = title.Trim();
        Detail = detail.Trim();
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public string CourseId { get; private set; } = null!;

    public string AssignmentId { get; private set; } = null!;

    /// <summary>The order within the assignment; the numbers on the page are counted from it.</summary>
    public int Position { get; private set; }

    public string Title { get; private set; } = null!;

    public string Detail { get; private set; } = null!;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Edit(string title, string detail, DateTimeOffset at)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException($"Note {Id} is deleted and cannot be edited.");
        }

        Title = title.Trim();
        Detail = detail.Trim();
        UpdatedAt = at.ToUniversalTime();
    }

    public void Delete(DateTimeOffset at)
    {
        IsDeleted = true;
        UpdatedAt = at.ToUniversalTime();
    }
}
