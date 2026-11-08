namespace TeachingAssistant.Domain.Ingestion;

public class InstructionsVersion
{
    private readonly List<InstructionsContent> _contents = [];

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

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public double? MaxPoints { get; private set; }

    public string StoragePath { get; private set; } = null!;
    public DateTimeOffset FetchedAt { get; private set; }
    public IReadOnlyList<InstructionsContent> Contents => _contents;
}
