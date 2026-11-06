namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// One snapshot of an assignment's instructions (v1, v2, …). Immutable once created: changed instructions become a new
/// version. Holds the task statement as it was then, and the attached files under StoragePath/original/.
/// </summary>
public class InstructionsVersion
{
    private readonly List<InstructionsContent> _contents = [];

    // For EF Core.
    private InstructionsVersion()
    {
    }

    internal InstructionsVersion(
        Guid instructionsId,
        int number,
        string title,
        string? description,
        double? maxPoints,
        string storagePath,
        DateTimeOffset fetchedAt,
        IEnumerable<InstructionsContent> contents)
    {
        Id = Guid.CreateVersion7();
        InstructionsId = instructionsId;
        Number = number;
        Title = title;
        Description = description;
        MaxPoints = maxPoints;
        StoragePath = storagePath;
        FetchedAt = fetchedAt.ToUniversalTime();
        _contents.AddRange(contents);
    }

    public Guid Id { get; private set; }
    public Guid InstructionsId { get; private set; }
    public int Number { get; private set; }

    /// <summary>The assignment's title in the source at the time of this version.</summary>
    public string Title { get; private set; } = null!;

    /// <summary>The task statement written in the source; null when there is none.</summary>
    public string? Description { get; private set; }

    /// <summary>The assignment's maximum points in the source; null for ungraded work.</summary>
    public double? MaxPoints { get; private set; }

    public string StoragePath { get; private set; } = null!;
    public DateTimeOffset FetchedAt { get; private set; }
    public IReadOnlyList<InstructionsContent> Contents => _contents;
}
