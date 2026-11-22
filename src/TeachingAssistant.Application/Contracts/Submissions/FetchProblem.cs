namespace TeachingAssistant.Application.Contracts;

public record FetchProblem
{
    public required string StudentName { get; init; }
    public required string Detail { get; init; }
}
