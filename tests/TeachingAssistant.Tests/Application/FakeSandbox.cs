using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Tests.Application;

/// <summary>A sandbox that records the request and answers with a scripted result, or is unavailable.</summary>
internal sealed class FakeSandbox : ISandboxRunner
{
    public SandboxRequest? Request { get; private set; }

    public SandboxResult? Result { get; set; }

    public string? Unavailable { get; set; }

    public Task<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken = default)
    {
        Request = request;
        if (Unavailable is not null)
        {
            throw new SandboxUnavailableException(Unavailable);
        }

        return Task.FromResult(Result ?? new SandboxResult
        {
            Steps = request.Steps.Select(s => new SandboxStepResult
            {
                Name = s.Name,
                Outcome = SandboxStepOutcome.Succeeded,
                ExitCode = 0,
                Stdout = "",
                Stderr = "",
                OutputTruncated = false,
                Duration = TimeSpan.Zero,
            }).ToList(),
            Duration = TimeSpan.Zero,
        });
    }
}
