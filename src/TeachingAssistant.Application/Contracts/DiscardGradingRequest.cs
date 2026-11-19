using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>Why the teacher discards a draft; the Evaluation page counts PoorGrading as the AI getting it wrong.</summary>
/// <param name="Reason">PoorGrading, WrongSubmission, NotNeeded or Other (Superseded is only set automatically).</param>
/// <param name="Note">Optional, in the teacher's words, at most 500 characters.</param>
public record DiscardGradingRequest(DiscardReason Reason, string? Note = null);
