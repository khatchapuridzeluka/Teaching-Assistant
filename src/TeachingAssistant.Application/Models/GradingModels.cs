using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.Application.Models;

/// <summary>
/// The models a grading can be run with instead of the configured one: in an evaluation run and with "Try with model".
/// </summary>
public static class GradingModels
{
    /// <summary>Most capable first.</summary>
    public static readonly IReadOnlyList<string> Available = ["claude-fable-5-2", "claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5"];

    public static void EnsureKnown(IEnumerable<string> models)
    {
        var unknown = models.Where(m => !Available.Contains(m)).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidRequestException($"Unknown model: {string.Join(", ", unknown)}. Choose from {string.Join(", ", Available)}.");
        }
    }
}
