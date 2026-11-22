namespace TeachingAssistant.Application.Jobs;

public record GradeAssignmentResult
{
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
    public required int Canceled { get; init; }
}
