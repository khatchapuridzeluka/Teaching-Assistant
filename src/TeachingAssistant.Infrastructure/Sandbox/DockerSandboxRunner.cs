using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Infrastructure.Sandbox;

/// <summary>
/// Runs sandbox steps in a throwaway Docker container, through the docker command line. Each run starts a fresh container
/// from a local image (never pulled), locked down: no network, a read-only file system with only /work and /tmp writable
/// (in memory), memory, CPU and process limits, no Linux capabilities and the image's unprivileged user. The files go in as
/// a tar stream, each step runs with docker exec under the image's own `timeout` (so an endless loop is stopped inside the
/// container), and the container is always removed afterwards. See sandbox/README.md for the image.
/// </summary>
public class DockerSandboxRunner : ISandboxRunner
{
    private const string WorkFolder = "/work";
    private const string SandboxUser = "10001";
    private const string ContainerLabel = "teaching-assistant.sandbox=true";

    // `timeout` exits with 124 when it stopped the command, and 137 when it had to kill it after the grace period.
    private const int TimeoutExitCode = 124;
    private const int KilledExitCode = 137;

    private static readonly TimeSpan KillGrace = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HostGrace = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MinimumStepTime = TimeSpan.FromMilliseconds(500);

    private readonly SandboxOptions _options;
    private readonly DockerCli _docker;
    private readonly ILogger<DockerSandboxRunner> _logger;

    public DockerSandboxRunner(IOptions<SandboxOptions> options, ILogger<DockerSandboxRunner> logger)
    {
        _options = options.Value;
        _docker = new DockerCli(_options.DockerPath);
        _logger = logger;
    }

    public async Task<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var started = Stopwatch.GetTimestamp();
        var limits = Resolve(request.Limits);

        var containerId = await StartAsync(request.Image, limits, cancellationToken);
        try
        {
            await CopyFilesAsync(containerId, request.Files, cancellationToken);
            var steps = await RunStepsAsync(containerId, request.Steps, limits, started, cancellationToken);
            var duration = Stopwatch.GetElapsedTime(started);
            _logger.LogInformation("Sandbox run in {Image} finished in {Duration} ms: {Outcomes}", request.Image,
                (int)duration.TotalMilliseconds, string.Join(", ", steps.Select(s => $"{s.Name}={s.Outcome}")));
            return new SandboxResult { Steps = steps, Duration = duration };
        }
        finally
        {
            await RemoveAsync(containerId);
        }
    }

    private async Task<string> StartAsync(string image, Limits limits, CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "run", "--detach", "--pull", "never", "--label", ContainerLabel,
            "--hostname", "localhost", "--network", "none", "--read-only",
            "--tmpfs", $"{WorkFolder}:rw,exec,size={_options.WorkFolderMb}m,uid={SandboxUser},gid={SandboxUser},mode=0700",
            "--tmpfs", $"/tmp:rw,size={_options.WorkFolderMb}m,mode=1777",
            "--memory", $"{limits.MemoryMb}m", "--memory-swap", $"{limits.MemoryMb}m",
            "--cpus", limits.Cpus.ToString(CultureInfo.InvariantCulture),
            "--pids-limit", _options.MaxProcesses.ToString(CultureInfo.InvariantCulture),
            "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
            "--user", SandboxUser, "--workdir", WorkFolder,
            image,
        ];

        var result = await _docker.RunAsync(arguments, null, _options.DockerCommandTimeout, 4096, cancellationToken);
        var containerId = result.Stdout.Trim();
        if (result.ExitCode == 0 && containerId.Length > 0)
        {
            return containerId;
        }

        throw new SandboxUnavailableException(StartFailure(image, result));
    }

    private static string StartFailure(string image, DockerCommandResult result)
    {
        if (result.HostTimedOut)
        {
            return "Docker did not start the sandbox container in time.";
        }

        var error = result.Stderr.Trim();
        if (error.Contains("No such image", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Unable to find image", StringComparison.OrdinalIgnoreCase))
        {
            return $"The sandbox image '{image}' is not built on this machine. Build it as described in sandbox/README.md.";
        }

        if (error.Contains("error during connect", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Cannot connect to the Docker daemon", StringComparison.OrdinalIgnoreCase)
            || error.Contains("docker daemon is not running", StringComparison.OrdinalIgnoreCase))
        {
            return "Docker is not running. Start Docker Desktop and try again.";
        }

        return $"Docker could not start the sandbox container: {FirstLine(error)}";
    }

    private async Task CopyFilesAsync(string containerId, IReadOnlyList<SandboxFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var archive = new MemoryStream();
        await using (var writer = new TarWriter(archive, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, NormalizePath(file.Path))
                {
                    DataStream = new MemoryStream(file.Content, writable: false),
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
                };
                await writer.WriteEntryAsync(entry, cancellationToken);
            }
        }

        string[] arguments = ["exec", "--interactive", containerId, "tar", "-xf", "-", "-C", WorkFolder];
        var result = await _docker.RunAsync(arguments, archive.ToArray(), _options.DockerCommandTimeout, 4096, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new SandboxUnavailableException(
                $"The files could not be copied into the sandbox: {FirstLine(result.HostTimedOut ? "timed out" : result.Stderr)}");
        }
    }

    private async Task<List<SandboxStepResult>> RunStepsAsync(
        string containerId, IReadOnlyList<SandboxStep> steps, Limits limits, long started, CancellationToken cancellationToken)
    {
        var results = new List<SandboxStepResult>();
        var skipRest = false;

        foreach (var step in steps)
        {
            var remaining = limits.TotalTimeout - Stopwatch.GetElapsedTime(started);
            if (skipRest || remaining < MinimumStepTime)
            {
                results.Add(new SandboxStepResult
                {
                    Name = step.Name,
                    Outcome = SandboxStepOutcome.Skipped,
                    Stdout = "",
                    Stderr = "",
                    OutputTruncated = false,
                    Duration = TimeSpan.Zero,
                });
                continue;
            }

            var timeout = Min(step.Timeout ?? _options.StepTimeout, remaining);
            var result = await RunStepAsync(containerId, step, timeout, limits.MaxOutputBytes, cancellationToken);
            results.Add(result);

            if (step.StopOnFailure && result.Outcome != SandboxStepOutcome.Succeeded)
            {
                skipRest = true;
            }
        }

        return results;
    }

    private async Task<SandboxStepResult> RunStepAsync(
        string containerId, SandboxStep step, TimeSpan timeout, int maxOutputBytes, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "exec" };
        if (step.Stdin is not null)
        {
            arguments.Add("--interactive");
        }

        arguments.AddRange([containerId, "timeout", "--kill-after", Seconds(KillGrace), Seconds(timeout)]);
        arguments.AddRange(step.Command);

        var stdin = step.Stdin is null ? null : new UTF8Encoding(false).GetBytes(step.Stdin);
        var stepStarted = Stopwatch.GetTimestamp();
        var result = await _docker.RunAsync(arguments, stdin, timeout + KillGrace + HostGrace, maxOutputBytes, cancellationToken);
        var duration = Stopwatch.GetElapsedTime(stepStarted);

        var timedOut = result.HostTimedOut
                       || result.ExitCode == TimeoutExitCode
                       || (result.ExitCode == KilledExitCode && duration >= timeout);
        var outcome = timedOut
            ? SandboxStepOutcome.TimedOut
            : result.ExitCode == 0 ? SandboxStepOutcome.Succeeded : SandboxStepOutcome.Failed;

        return new SandboxStepResult
        {
            Name = step.Name,
            Outcome = outcome,
            ExitCode = timedOut ? null : result.ExitCode,
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            OutputTruncated = result.OutputTruncated,
            Duration = duration,
        };
    }

    private async Task RemoveAsync(string containerId)
    {
        try
        {
            string[] arguments = ["rm", "--force", containerId];
            var result = await _docker.RunAsync(arguments, null, _options.DockerCommandTimeout, 4096, CancellationToken.None);
            if (result.ExitCode != 0)
            {
                _logger.LogWarning("Sandbox container {ContainerId} could not be removed: {Error}", containerId, FirstLine(result.Stderr));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sandbox container {ContainerId} could not be removed", containerId);
        }
    }

    private Limits Resolve(SandboxLimits? limits)
    {
        return new Limits
        {
            MemoryMb = limits?.MemoryMb ?? _options.MemoryMb,
            Cpus = limits?.Cpus ?? _options.Cpus,
            TotalTimeout = limits?.TotalTimeout ?? _options.TotalTimeout,
            MaxOutputBytes = limits?.MaxOutputBytes ?? _options.MaxOutputBytes,
        };
    }

    private static void Validate(SandboxRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Image))
        {
            throw new ArgumentException("The sandbox request needs an image.", nameof(request));
        }

        foreach (var step in request.Steps)
        {
            if (step.Command.Count == 0 || string.IsNullOrWhiteSpace(step.Command[0]))
            {
                throw new ArgumentException($"The sandbox step '{step.Name}' has no command.", nameof(request));
            }
        }

        foreach (var file in request.Files)
        {
            NormalizePath(file.Path);
        }
    }

    /// <summary>A relative path inside the working folder; refuses absolute paths and any way out of it ("..").</summary>
    internal static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (normalized.Length == 0
            || normalized.StartsWith('/')
            || normalized.Contains(':')
            || segments.Any(s => s is "" or "." or ".."))
        {
            throw new ArgumentException($"'{path}' is not a relative path inside the sandbox's working folder.", nameof(path));
        }

        return normalized;
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b)
    {
        return a < b ? a : b;
    }

    private static string Seconds(TimeSpan value)
    {
        return value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";
    }

    private static string FirstLine(string text)
    {
        var line = text.Trim().Split('\n')[0].Trim();
        return line.Length == 0 ? "no details" : line;
    }

    private sealed record Limits
    {
        public required int MemoryMb { get; init; }
        public required double Cpus { get; init; }
        public required TimeSpan TotalTimeout { get; init; }
        public required int MaxOutputBytes { get; init; }
    }
}
