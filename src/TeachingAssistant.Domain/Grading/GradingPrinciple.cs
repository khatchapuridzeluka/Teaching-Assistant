namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// A principle of how the grader works, the same for every course: a short title for people and the exact instruction the
/// grader gets. Deleting only marks it deleted, so earlier gradings can still be explained.
/// </summary>
public class GradingPrinciple
{
    public const int MaxTitleLength = 120;
    public const int MaxDetailLength = 2000;

    private GradingPrinciple()
    {
    }

    public GradingPrinciple(Guid id, int position, string title, string detail, DateTimeOffset createdAt)
    {
        Id = id;
        Position = position;
        Title = title.Trim();
        Detail = detail.Trim();
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    /// <summary>The order the grader reads the principles in; the numbers on the page are counted from it.</summary>
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
            throw new InvalidOperationException($"Principle {Id} is deleted and cannot be edited.");
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
