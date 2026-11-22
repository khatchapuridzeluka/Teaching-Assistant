using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Contracts;

public record ModelUsageResponse
{
    public required DateTimeOffset Since { get; init; }
    public ModelCallPurpose? Purpose { get; init; }
    public required ModelUsageSummary Summary { get; init; }
    public required IReadOnlyList<ModelUsageTotal> Totals { get; init; }
    public required IReadOnlyList<ModelUsageDay> Days { get; init; }
    public required IReadOnlyList<ModelCallResponse> Recent { get; init; }
}
