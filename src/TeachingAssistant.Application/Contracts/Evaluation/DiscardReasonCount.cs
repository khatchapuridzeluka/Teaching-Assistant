using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record DiscardReasonCount
{
    public DiscardReason? Reason { get; init; }
    public required int Count { get; init; }
}
