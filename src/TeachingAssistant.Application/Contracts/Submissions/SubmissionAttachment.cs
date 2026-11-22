namespace TeachingAssistant.Application.Contracts;

public record SubmissionAttachment
{
    public required AttachmentKind Kind { get; init; }
    public required string Title { get; init; }
    public string? Url { get; init; }
    public string? FileId { get; init; }
}
