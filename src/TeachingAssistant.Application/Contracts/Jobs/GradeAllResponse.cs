namespace TeachingAssistant.Application.Contracts;

public record GradeAllResponse
{
    public JobResponse? Job { get; init; }
    public required int Queued { get; init; }
    public required int Students { get; init; }
    public required int AlreadyDrafted { get; init; }
    public required int AlreadyQueued { get; init; }
    public required int NoWork { get; init; }
    public required int NotFetched { get; init; }
    public required IReadOnlyList<string> Problems { get; init; }
}
