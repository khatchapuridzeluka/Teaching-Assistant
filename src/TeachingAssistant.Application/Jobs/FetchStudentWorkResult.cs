namespace TeachingAssistant.Application.Jobs;

public record FetchStudentWorkResult
{
    public required int Fetched { get; init; }
    public required int Queued { get; init; }
    public required int AlreadyDrafted { get; init; }
    public required int AlreadyQueued { get; init; }
    public required int NoWork { get; init; }
    public required int NotFetched { get; init; }
    public required IReadOnlyList<string> Problems { get; init; }
}
