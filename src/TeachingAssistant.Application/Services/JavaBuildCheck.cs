using System.Text;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Sandbox;

namespace TeachingAssistant.Application.Services;

/// <summary>What the compile check found: its status and the compiler's messages (or why it did not run).</summary>
/// <param name="Status">Whether it compiled, failed, timed out, had nothing to compile or could not run.</param>
/// <param name="Output">The compiler's messages, cut to a few thousand characters; for Skipped and Unavailable, the reason.</param>
/// <param name="Programs">How many programs (compile units) were compiled.</param>
/// <param name="Warnings">How many compiler warnings there were.</param>
public record BuildCheckOutcome(BuildStatus Status, string Output, int Programs = 0, int Warnings = 0);

/// <summary>
/// Compiles a submission's Java files in the sandbox before it is graded, so the grader gets "it compiles" or the
/// compiler's errors as facts instead of judging that from the code. Every submission gets the same check. The files are
/// grouped into programs (see JavaCompileUnits) and each is compiled on its own with javac; nothing is run yet.
/// </summary>
public class JavaBuildCheck
{
    public const string Image = "ta-java:27";
    public const string JdkName = "JDK 27";

    // javac's warnings that point at real beginner problems: unsafe or raw generics, deprecated APIs, a switch case that
    // falls through, a static member used through an object, a cast that does nothing, a constant division by zero, an
    // empty statement after if, and equals without hashCode. Noise such as serial or annotation processing stays off.
    public const string Warnings = "-Xlint:unchecked,deprecation,rawtypes,fallthrough,static,cast,divzero,empty,overrides";

    private const int MaxFileBytes = 200_000;
    private const int MaxOutputChars = 6_000;

    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(30);
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly ISandboxRunner _sandbox;
    private readonly ISnapshotStorage _storage;

    public JavaBuildCheck(ISandboxRunner sandbox, ISnapshotStorage storage)
    {
        _sandbox = sandbox;
        _storage = storage;
    }

    public async Task<BuildCheckOutcome> CheckAsync(SubmissionVersion version, CancellationToken cancellationToken = default)
    {
        var sources = ReadSources(version);
        var units = JavaCompileUnits.Plan(sources.Select(s => new JavaSourceFile(s.Path, s.Text)));
        if (units.Count == 0)
        {
            return new BuildCheckOutcome(BuildStatus.Skipped, "The submission has no Java source files.");
        }

        var files = sources.Select(s => new SandboxFile(s.Path, s.Bytes)).ToList();
        var steps = new List<SandboxStep>();
        for (var i = 0; i < units.Count; i++)
        {
            // An argument file keeps the command short however many files there are; javac reads it with @.
            var argumentFile = $"__ta_sources_{i + 1}.txt";
            files.Add(new SandboxFile(argumentFile,
                new UTF8Encoding(false).GetBytes(string.Join("\n", units[i].Files.Select(f => $"\"{f}\"")) + "\n")));
            steps.Add(new SandboxStep(Label(units[i]),
            [
                "javac", "-encoding", "UTF-8", Warnings, "-Xmaxerrs", "50", "-Xmaxwarns", "50",
                "-d", $"/tmp/classes-{i + 1}", "@" + argumentFile,
            ]));
        }

        SandboxResult result;
        try
        {
            result = await _sandbox.RunAsync(
                new SandboxRequest(Image, files, steps, new SandboxLimits(TotalTimeout: TotalTimeout)), cancellationToken);
        }
        catch (SandboxUnavailableException ex)
        {
            return new BuildCheckOutcome(BuildStatus.Unavailable, ex.Message, units.Count);
        }

        return Summarize(units, result);
    }

    /// <summary>
    /// The facts for the grading prompt, or null when there is nothing to say (no Java files, or the check could not run, in
    /// which case the grader judges from the code as it always did).
    /// </summary>
    public static string? PromptSection(BuildCheckOutcome outcome)
    {
        var verdict = outcome.Status switch
        {
            BuildStatus.Compiled => outcome.Warnings == 0
                ? "Result: it compiled without errors."
                : $"Result: it compiled without errors, with {outcome.Warnings} compiler warning{(outcome.Warnings == 1 ? "" : "s")}.",
            BuildStatus.CompileFailed => "Result: it did NOT compile.",
            BuildStatus.TimedOut => $"Result: compiling took longer than {TotalTimeout.TotalSeconds:0} seconds and was stopped, so whether it compiles is unknown.",
            _ => null,
        };
        if (verdict is null)
        {
            return null;
        }

        var section = new StringBuilder("<build_and_test_results>\n")
            .Append("The tool compiled the submission's Java files with javac (").Append(JdkName)
            .Append(") in a sandbox before this review; this is a fact about the code, not a guess.");
        if (outcome.Programs > 1)
        {
            section.Append(" The files form ").Append(outcome.Programs).Append(" separate programs, each compiled on its own.");
        }

        section.Append('\n').Append(verdict).Append('\n');
        if (outcome.Output.Length > 0)
        {
            section.Append("Compiler output (paths as in the submission):\n").Append(outcome.Output).Append('\n');
        }

        return section.Append("</build_and_test_results>").ToString();
    }

    private static BuildCheckOutcome Summarize(IReadOnlyList<JavaCompileUnit> units, SandboxResult result)
    {
        var steps = result.Steps;
        var status = steps.Any(s => s.Outcome is SandboxStepOutcome.TimedOut or SandboxStepOutcome.Skipped)
            ? BuildStatus.TimedOut
            : steps.Any(s => s.Outcome == SandboxStepOutcome.Failed) ? BuildStatus.CompileFailed : BuildStatus.Compiled;

        var output = new StringBuilder();
        foreach (var step in steps)
        {
            var messages = (step.Stderr + step.Stdout).Trim();
            if (messages.Length == 0)
            {
                continue;
            }

            if (units.Count > 1)
            {
                output.Append("[").Append(step.Name).Append("]\n");
            }

            output.Append(messages).Append('\n');
            if (step.OutputTruncated)
            {
                output.Append("(more compiler output was cut off)\n");
            }
        }

        var text = output.ToString().TrimEnd();
        if (text.Length > MaxOutputChars)
        {
            text = text[..MaxOutputChars] + "\n(more compiler output was cut off)";
        }

        var warnings = steps.Sum(s => CountWarnings(s.Stderr));
        return new BuildCheckOutcome(status, text, units.Count, warnings);
    }

    private static int CountWarnings(string output)
    {
        return output.Split('\n').Count(l => l.Contains(": warning:", StringComparison.Ordinal));
    }

    private static string Label(JavaCompileUnit unit)
    {
        return unit.Root.Length == 0 ? "compile" : $"compile {unit.Root}/";
    }

    private List<(string Path, byte[] Bytes, string Text)> ReadSources(SubmissionVersion version)
    {
        var sources = new List<(string Path, byte[] Bytes, string Text)>();
        foreach (var file in _storage.ListCode(version.StoragePath))
        {
            if (!file.Path.EndsWith(".java", StringComparison.OrdinalIgnoreCase) || file.SizeBytes > MaxFileBytes)
            {
                continue;
            }

            using var stream = _storage.OpenCode(version.StoragePath, file.Path);
            if (stream is null)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();

            // javac rejects a UTF-8 byte order mark ("illegal character '﻿'") that Windows editors often write, while
            // the student's IDE accepts it; dropping it keeps that from counting as a compile error.
            if (bytes.AsSpan().StartsWith(Utf8Bom))
            {
                bytes = bytes[Utf8Bom.Length..];
            }

            sources.Add((file.Path.Replace('\\', '/'), bytes, new UTF8Encoding(false).GetString(bytes)));
        }

        return sources;
    }
}
