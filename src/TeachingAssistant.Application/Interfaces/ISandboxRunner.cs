using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Runs commands on a student's files in an isolated, throwaway container, so student code never runs on the host. It knows
/// nothing about Java or grading: it copies the files in, runs the steps in order, returns what each step printed and
/// removes the container. Throws SandboxUnavailableException when the sandbox cannot run at all (Docker not running, the
/// image not built); a step that fails, times out or prints too much is a normal result, not an exception.
/// </summary>
public interface ISandboxRunner
{
    /// <summary>Runs the request's steps in one fresh container and returns each step's outcome, in order.</summary>
    Task<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken = default);
}
