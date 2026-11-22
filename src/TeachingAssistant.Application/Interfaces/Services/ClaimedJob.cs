using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Interfaces.Services;

public record ClaimedJob
{
    public required Guid Id { get; init; }
    public required JobType Type { get; init; }
    public required string Payload { get; init; }
}
