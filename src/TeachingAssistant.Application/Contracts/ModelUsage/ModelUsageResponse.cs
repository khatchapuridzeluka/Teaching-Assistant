using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Contracts;

public record ModelUsageResponse(
    DateTimeOffset Since,
    ModelCallPurpose? Purpose,
    ModelUsageSummary Summary,
    IReadOnlyList<ModelUsageTotal> Totals,
    IReadOnlyList<ModelUsageDay> Days,
    IReadOnlyList<ModelCallResponse> Recent);
