using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.Infrastructure.Sandbox;

/// <summary>What one docker command did: its exit code and the start of what it printed.</summary>
internal sealed record DockerCommandResult
{
    /// <summary>The exit code; -1 when it was stopped from the host for taking too long.</summary>
    public required int ExitCode { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }

    public required bool OutputTruncated { get; init; }

    /// <summary>True when the host stopped the command because it took too long.</summary>
    public required bool HostTimedOut { get; init; }
}

/// <summary>
/// Runs the docker command line program with arguments passed one by one (never through a shell, so nothing in them is
/// interpreted), optional bytes on its standard input, a time limit and a cap on how much output is kept.
/// </summary>
internal sealed class DockerCli
{
    private readonly string _dockerPath;

    public DockerCli(string dockerPath)
    {
        _dockerPath = dockerPath;
    }

    public async Task<DockerCommandResult> RunAsync(
        IReadOnlyList<string> arguments, byte[]? stdin, TimeSpan timeout, int maxOutputBytes, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_dockerPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new SandboxUnavailableException(
                $"The docker program could not be started ('{_dockerPath}'). Is Docker installed and on the PATH?", ex);
        }

        var stdout = new CappedOutput(maxOutputBytes);
        var stderr = new CappedOutput(maxOutputBytes);
        var reading = Task.WhenAll(
            stdout.ReadToEndAsync(process.StandardOutput.BaseStream),
            stderr.ReadToEndAsync(process.StandardError.BaseStream));

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        var hostTimedOut = false;
        try
        {
            await WriteInputAsync(process, stdin, limit.Token);
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            hostTimedOut = true;
        }

        try
        {
            await reading.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch (TimeoutException)
        {
            // A child process still holds the pipe open; keep what was read so far.
        }

        return new DockerCommandResult
        {
            ExitCode = hostTimedOut ? -1 : process.ExitCode,
            Stdout = stdout.Text(),
            Stderr = stderr.Text(),
            OutputTruncated = stdout.Truncated || stderr.Truncated,
            HostTimedOut = hostTimedOut,
        };
    }

    private static async Task WriteInputAsync(Process process, byte[]? stdin, CancellationToken cancellationToken)
    {
        try
        {
            if (stdin is { Length: > 0 })
            {
                await process.StandardInput.BaseStream.WriteAsync(stdin, cancellationToken);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The command ended without reading all of its input (a program that never reads stdin); not an error.
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    /// <summary>Keeps the first bytes of a stream and drains the rest, so a program that prints endlessly cannot block or fill memory.</summary>
    private sealed class CappedOutput(int maxBytes)
    {
        private readonly MemoryStream _kept = new();

        public bool Truncated { get; private set; }

        public async Task ReadToEndAsync(Stream stream)
        {
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                var room = maxBytes - (int)_kept.Length;
                if (room > 0)
                {
                    _kept.Write(buffer, 0, Math.Min(room, read));
                }

                if (read > room)
                {
                    Truncated = true;
                }
            }
        }

        public string Text()
        {
            return Encoding.UTF8.GetString(_kept.GetBuffer(), 0, (int)_kept.Length);
        }
    }
}
