namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// One of the teacher's grading rules: a judgement call the grader must apply the same way every time. Title, what it
/// covers and its effect are for people; Detail is the exact instruction the grader gets. Deleting only marks it deleted,
/// so earlier gradings can still be explained.
/// </summary>
public class PolicyRule
{
    public const int MaxTitleLength = 120;
    public const int MaxCoversLength = 300;
    public const int MaxPointsPerLength = 40;
    public const int MaxDetailLength = 2000;

    private PolicyRule()
    {
    }

    public PolicyRule(
        Guid id, int position, string title, PolicyEffect effect, double? points, string? pointsPer, string covers, string detail,
        DateTimeOffset createdAt)
    {
        Id = id;
        Position = position;
        CreatedAt = createdAt.ToUniversalTime();
        Set(title, effect, points, pointsPer, covers, detail, CreatedAt);
    }

    public Guid Id { get; private set; }

    /// <summary>The order the grader reads the rules in; the numbers on the page are counted from it.</summary>
    public int Position { get; private set; }

    public string Title { get; private set; } = null!;

    public PolicyEffect Effect { get; private set; }

    /// <summary>The points a Deduction costs; null for NoDeduction.</summary>
    public double? Points { get; private set; }

    /// <summary>What the points are counted per, for example "per name"; null when the deduction is once.</summary>
    public string? PointsPer { get; private set; }

    public string Covers { get; private set; } = null!;

    public string Detail { get; private set; } = null!;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Edit(string title, PolicyEffect effect, double? points, string? pointsPer, string covers, string detail, DateTimeOffset at)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException($"Rule {Id} is deleted and cannot be edited.");
        }

        Set(title, effect, points, pointsPer, covers, detail, at.ToUniversalTime());
    }

    public void Delete(DateTimeOffset at)
    {
        IsDeleted = true;
        UpdatedAt = at.ToUniversalTime();
    }

    private void Set(string title, PolicyEffect effect, double? points, string? pointsPer, string covers, string detail, DateTimeOffset at)
    {
        Title = title.Trim();
        Effect = effect;
        Points = effect == PolicyEffect.Deduction ? points : null;
        PointsPer = effect == PolicyEffect.Deduction && !string.IsNullOrWhiteSpace(pointsPer) ? pointsPer.Trim() : null;
        Covers = covers.Trim();
        Detail = detail.Trim();
        UpdatedAt = at;
    }
}
