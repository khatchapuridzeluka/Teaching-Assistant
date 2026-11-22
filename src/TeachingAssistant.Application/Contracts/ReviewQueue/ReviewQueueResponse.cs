namespace TeachingAssistant.Application.Contracts;

public record ReviewQueueResponse(
    IReadOnlyList<ReviewDraftItem> Drafts,
    IReadOnlyList<UngradedSubmissionItem> Ungraded);
