using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Jobs;

public record GenerateTestsPayload
{
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
    public required GenerateTestsRequest Request { get; init; }
}
