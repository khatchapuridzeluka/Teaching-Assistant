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
/// <param name="StyleFindings">How many Checkstyle findings there were; null when Checkstyle did not run.</param>
/// <param name="StyleOutput">Checkstyle's findings, one per line with the submission's paths, or why it could not finish.</param>
/// <param name="PmdFindings">How many PMD findings there were; null when PMD did not run.</param>
/// <param name="PmdOutput">PMD's findings, one per line with the submission's paths, or why it could not finish.</param>
public record BuildCheckOutcome(
    BuildStatus Status,
    string Output,
    int Programs = 0,
    int Warnings = 0,
    int? StyleFindings = null,
    string StyleOutput = "",
    int? PmdFindings = null,
    string PmdOutput = "");

/// <summary>
/// Compiles a submission's Java files in the sandbox before it is graded, so the grader gets "it compiles" or the
/// compiler's errors as facts instead of judging that from the code. Every submission gets the same check. The files are
/// grouped into programs (see JavaCompileUnits) and each is compiled on its own with javac. Then Checkstyle reads all of
/// them with the teacher's short rule list (Checks/checkstyle.xml), and PMD with another short list (Checks/pmd.xml); both
/// read source, so they run even when compiling fails. Nothing is run yet.
/// </summary>
public class JavaBuildCheck
{
    public const string Image = "ta-java:27";
    public const string JdkName = "JDK 27";

    // javac's warnings that point at real beginner problems: unsafe or raw generics, deprecated APIs, a switch case that
    // falls through, a static member used through an object, a cast that does nothing, a constant division by zero, an
    // empty statement after if, and equals without hashCode. Noise such as serial or annotation processing stays off.
    public const string Warnings = "-Xlint:unchecked,deprecation,rawtypes,fallthrough,static,cast,divzero,empty,overrides";

    /// <summary>What Checkstyle checks, in words for the grader; keep it in step with Checks/checkstyle.xml.</summary>
    public const string StyleRules =
        "naming conventions, non-private fields, unused imports, missing braces, strings compared with ==, empty catch " +
        "blocks, switch without default, and boolean logic that can be simplified";

    /// <summary>What PMD checks, in words for the grader; keep it in step with Checks/pmd.xml.</summary>
    public const string PmdRules =
        "resources such as a Scanner that are never closed, objects compared with ==, unused local variables, private " +
        "fields and private methods, code that is too complex or too deeply nested, fields that should be local variables, " +
        "and strings built with += in a loop";

    private const string CheckstyleStep = "checkstyle";
    private const string CheckstyleConfig = "__ta_checkstyle.xml";
    private const string PmdStep = "pmd";
    private const string PmdConfig = "__ta_pmd.xml";
    private const int MaxFileBytes = 200_000;
    private const int MaxOutputChars = 6_000;
    private const int MaxStyleChars = 4_000;

    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(60);
    private static readonly Lazy<byte[]> CheckstyleRules = new(() => LoadRules("checkstyle.xml"));
    private static readonly Lazy<byte[]> PmdRuleSet = new(() => LoadRules("pmd.xml"));
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

        // Checkstyle reads source, so it runs whatever the compiler said; "." is the working folder with the student's files.
        files.Add(new SandboxFile(CheckstyleConfig, CheckstyleRules.Value));
        steps.Add(new SandboxStep(CheckstyleStep, ["checkstyle", "-c", CheckstyleConfig, "."]));

        // PMD reads source too. The compiled classes, where compiling worked, let it resolve types (for example that a
        // variable is a Scanner); a missing class folder is only a warning to PMD.
        files.Add(new SandboxFile(PmdConfig, PmdRuleSet.Value));
        var classes = string.Join(":", Enumerable.Range(1, units.Count).Select(i => $"/tmp/classes-{i}"));
        steps.Add(new SandboxStep(PmdStep,
            ["pmd", "check", "--no-progress", "--no-cache", "-d", ".", "-R", PmdConfig, "-f", "text", "--aux-classpath", classes]));

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
            BuildStatus.TimedOut => "Result: compiling took too long and was stopped, so whether it compiles is unknown.",
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

        section.Append("Checkstyle (a style checker with the teacher's short list: ").Append(StyleRules).Append(") ");
        if (outcome.StyleFindings is not { } findings)
        {
            section.Append("did not finish").Append(outcome.StyleOutput.Length > 0 ? ": " + outcome.StyleOutput : ".").Append('\n');
        }
        else if (findings == 0)
        {
            section.Append("found nothing.\n");
            AppendStyleNote(section, outcome.StyleOutput);
        }
        else
        {
            section.Append("found ").Append(findings).Append(findings == 1 ? " issue" : " issues")
                .Append(" (file:line:column: message [rule]):\n").Append(outcome.StyleOutput).Append('\n');
        }

        section.Append("PMD (a code checker with the teacher's short list: ").Append(PmdRules).Append(") ");
        if (outcome.PmdFindings is not { } pmdFindings)
        {
            section.Append("did not finish").Append(outcome.PmdOutput.Length > 0 ? ": " + outcome.PmdOutput : ".").Append('\n');
        }
        else if (pmdFindings == 0)
        {
            section.Append("found nothing.\n");
            AppendStyleNote(section, outcome.PmdOutput);
        }
        else
        {
            section.Append("found ").Append(pmdFindings).Append(pmdFindings == 1 ? " issue" : " issues")
                .Append(" (file:line: message [rule]):\n").Append(outcome.PmdOutput).Append('\n');
        }

        return section.Append("</build_and_test_results>").ToString();
    }

    private static void AppendStyleNote(StringBuilder section, string note)
    {
        if (note.Length > 0)
        {
            section.Append(note).Append('\n');
        }
    }

    private static BuildCheckOutcome Summarize(IReadOnlyList<JavaCompileUnit> units, SandboxResult result)
    {
        var steps = result.Steps.Where(s => s.Name is not (CheckstyleStep or PmdStep)).ToList();
        var (styleFindings, styleOutput) = ReadCheckstyle(result.Steps.FirstOrDefault(s => s.Name == CheckstyleStep));
        var (pmdFindings, pmdOutput) = ReadPmd(result.Steps.FirstOrDefault(s => s.Name == PmdStep));
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
        return new BuildCheckOutcome(status, text, units.Count, warnings, styleFindings, styleOutput, pmdFindings, pmdOutput);
    }

    /// <summary>
    /// PMD's findings as "path:line: message [Rule]" lines with the submission's own paths. PMD exits with 0 (nothing found),
    /// 4 (findings) or 5 (findings, but some files could not be parsed); those parse errors are counted in a note. Null
    /// findings when it did not finish: it timed out, was skipped or stopped with another exit code.
    /// </summary>
    internal static (int? Findings, string Output) ReadPmd(SandboxStepResult? step)
    {
        if (step is null || step.Outcome is SandboxStepOutcome.Skipped)
        {
            return (null, "");
        }

        if (step.Outcome is SandboxStepOutcome.TimedOut)
        {
            return (null, "it took too long and was stopped");
        }

        if (step.ExitCode is not (0 or 4 or 5))
        {
            var reason = step.Stderr.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("[ERROR]", StringComparison.Ordinal))
                         ?? $"it stopped with exit code {step.ExitCode}";
            return (null, reason);
        }

        var findings = new List<string>();
        var unreadable = 0;
        foreach (var line in step.Stdout.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 3 && parts[0].EndsWith(':') && parts[1].EndsWith(':'))
            {
                // "./src/Main.java:10:", "CloseResource:", "Ensure that resources like this Scanner object are closed after use"
                findings.Add($"{SubmissionPath(parts[0])} {string.Join(" ", parts[2..]).Trim()} [{parts[1].TrimEnd(':')}]");
            }
            else if (parts.Length >= 3 && parts[1] == "-")
            {
                unreadable++;
            }
        }

        var text = string.Join("\n", findings);
        if (text.Length > MaxStyleChars)
        {
            var kept = text[..MaxStyleChars];
            text = kept[..Math.Max(0, kept.LastIndexOf('\n'))] + "\n(more findings were cut off)";
        }

        if (unreadable > 0)
        {
            text += (text.Length > 0 ? "\n" : "") +
                    $"({unreadable} file{(unreadable == 1 ? "" : "s")} could not be read by PMD, usually because of a syntax error)";
        }

        return (findings.Count, text);
    }

    /// <summary>
    /// Checkstyle's findings as "path:line:column: message [Rule]" lines with the submission's own paths. Files it could not
    /// parse (syntax errors, which the compiler reports anyway) are counted in a note instead of their stack traces. Null
    /// findings when it did not finish: it timed out, was skipped, or never got to "Audit done".
    /// </summary>
    internal static (int? Findings, string Output) ReadCheckstyle(SandboxStepResult? step)
    {
        if (step is null || step.Outcome is SandboxStepOutcome.Skipped)
        {
            return (null, "");
        }

        if (step.Outcome is SandboxStepOutcome.TimedOut)
        {
            return (null, "it took too long and was stopped");
        }

        if (!step.Stdout.Contains("Audit done.", StringComparison.Ordinal))
        {
            var reason = (step.Stderr + "\n" + step.Stdout).Trim().Split('\n')[0].Trim();
            return (null, reason.Length == 0 ? "" : reason);
        }

        var findings = new List<string>();
        var unreadable = 0;
        foreach (var line in step.Stdout.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.StartsWith("[WARN] ", StringComparison.Ordinal))
            {
                findings.Add(SubmissionPath(line["[WARN] ".Length..]));
            }
            else if (line.StartsWith("[ERROR] ", StringComparison.Ordinal))
            {
                unreadable++;
            }
        }

        var text = string.Join("\n", findings);
        if (text.Length > MaxStyleChars)
        {
            var kept = text[..MaxStyleChars];
            text = kept[..Math.Max(0, kept.LastIndexOf('\n'))] + "\n(more findings were cut off)";
        }

        if (unreadable > 0)
        {
            text += (text.Length > 0 ? "\n" : "") +
                    $"({unreadable} file{(unreadable == 1 ? "" : "s")} could not be read by Checkstyle, usually because of a syntax error)";
        }

        return (findings.Count, text);
    }

    private static string SubmissionPath(string finding)
    {
        foreach (var prefix in new[] { "/work/./", "/work/", "./" })
        {
            if (finding.StartsWith(prefix, StringComparison.Ordinal))
            {
                return finding[prefix.Length..];
            }
        }

        return finding;
    }

    private static byte[] LoadRules(string fileName)
    {
        using var stream = typeof(JavaBuildCheck).Assembly.GetManifestResourceStream("TeachingAssistant.Application.Checks." + fileName)
                           ?? throw new InvalidOperationException($"Checks/{fileName} is not embedded.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
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

            // javac rejects a UTF-8 byte order mark ("illegal character '\ufeff'") that Windows editors often write, while
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
