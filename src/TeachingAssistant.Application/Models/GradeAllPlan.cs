namespace TeachingAssistant.Application.Models;

public record GradeAllPlan
{
    public required IReadOnlyList<Guid> VersionIds { get; init; }
    public required int Students { get; init; }
    public required int AlreadyDrafted { get; init; }
    public required int NoWork { get; init; }
    public required IReadOnlyList<string> Problems { get; init; }
    public IReadOnlyDictionary<Guid, string>? AssignmentIds { get; init; }
    public int NotFetched { get; init; }
}
