namespace TeachingAssistant.Application.Contracts;

public record ReviewQueueResponse
{
    public required IReadOnlyList<ReviewDraftItem> Drafts { get; init; }
    public required IReadOnlyList<UngradedSubmissionItem> Ungraded { get; init; }
}
