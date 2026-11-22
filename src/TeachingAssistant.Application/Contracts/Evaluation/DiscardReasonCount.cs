using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record DiscardReasonCount(DiscardReason? Reason, int Count);
