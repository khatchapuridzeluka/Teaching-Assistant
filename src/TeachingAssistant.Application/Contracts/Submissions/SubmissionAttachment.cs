namespace TeachingAssistant.Application.Contracts;

public record SubmissionAttachment(
    AttachmentKind Kind,
    string Title,
    string? Url,
    string? FileId);
