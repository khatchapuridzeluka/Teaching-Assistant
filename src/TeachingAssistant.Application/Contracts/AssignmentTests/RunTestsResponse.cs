namespace TeachingAssistant.Application.Contracts;

public record RunTestsResponse
{
    /// <summary>How many students' latest versions were queued for a test run.</summary>
    public required int Queued { get; init; }

    /// <summary>Why some students were left out, one line each.</summary>
    public required IReadOnlyList<string> Skipped { get; init; }
}
