using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>Why the teacher discards a draft; the Evaluation page counts PoorGrading as the AI getting it wrong.</summary>
public record DiscardGradingRequest
{
    /// <summary>PoorGrading, WrongSubmission, NotNeeded or Other (Superseded is only set automatically).</summary>
    public required DiscardReason Reason { get; init; }

    /// <summary>Optional, in the teacher's words, at most 500 characters.</summary>
    public string? Note { get; init; }
}
