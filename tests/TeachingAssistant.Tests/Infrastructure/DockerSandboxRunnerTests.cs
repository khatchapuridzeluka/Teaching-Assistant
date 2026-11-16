using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Infrastructure.Sandbox;

namespace TeachingAssistant.Tests.Infrastructure;

/// <summary>
/// Runs against the real Docker and the ta-java:27 image (sandbox/README.md). Skipped when Docker is not running or the
/// image is not built, so the suite still passes on a machine without them.
/// </summary>
public class DockerSandboxRunnerTests
{
    private const string Image = "ta-java:27";

    private static readonly Lazy<string?> Unavailable = new(CheckDocker);

    private static DockerSandboxRunner Runner(SandboxOptions? options = null)
    {
        return new DockerSandboxRunner(Options.Create(options ?? new SandboxOptions()), NullLogger<DockerSandboxRunner>.Instance);
    }

    private static SandboxFile File(string path, string text)
    {
        return new SandboxFile(path, Encoding.UTF8.GetBytes(text));
    }

    private static void SkipWithoutDocker()
    {
        Assert.SkipWhen(Unavailable.Value is not null, Unavailable.Value ?? "");
    }

    [Fact]
    public async Task CompilesAndRunsAProgramInAPackage_WithStdinAndGeorgianOutput()
    {
        SkipWithoutDocker();
        var request = new SandboxRequest(Image,
        [
            File("src/shop/Main.java", """
                package shop;

                import java.util.Scanner;

                public class Main {
                    public static void main(String[] args) {
                        int n = new Scanner(System.in).nextInt();
                        System.out.println("Factorial: " + new Calculator().factorial(n));
                        System.out.println("გამარჯობა");
                    }
                }
                """),
            File("src/shop/Calculator.java", """
                package shop;

                public class Calculator {
                    public long factorial(int n) {
                        long r = 1;
                        for (int i = 2; i <= n; i++) {
                            r *= i;
                        }
                        return r;
                    }
                }
                """),
        ],
        [
            new SandboxStep("compile", ["javac", "-d", "out", "src/shop/Main.java", "src/shop/Calculator.java"], StopOnFailure: true),
            new SandboxStep("run", ["java", "-cp", "out", "shop.Main"], Stdin: "5\n"),
        ]);

        var result = await Runner().RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal([SandboxStepOutcome.Succeeded, SandboxStepOutcome.Succeeded], result.Steps.Select(s => s.Outcome));
        Assert.Equal("Factorial: 120\nგამარჯობა\n", result.Steps[1].Stdout);
        Assert.Equal(0, result.Steps[1].ExitCode);
    }

    [Fact]
    public async Task ACompileError_FailsTheStep_AndSkipsTheStepsAfterIt()
    {
        SkipWithoutDocker();
        var request = new SandboxRequest(Image,
            [File("Main.java", "public class Main { void f() { int x = \"text\"; } }")],
            [
                new SandboxStep("compile", ["javac", "Main.java"], StopOnFailure: true),
                new SandboxStep("run", ["java", "Main"]),
            ]);

        var result = await Runner().RunAsync(request, TestContext.Current.CancellationToken);

        var compile = result.Steps[0];
        Assert.Equal(SandboxStepOutcome.Failed, compile.Outcome);
        Assert.NotEqual(0, compile.ExitCode);
        Assert.Contains("Main.java:1: error: incompatible types", compile.Stderr);
        Assert.Equal(SandboxStepOutcome.Skipped, result.Steps[1].Outcome);
    }

    [Fact]
    public async Task AnEndlessLoop_TimesOut_AndTheNextStepStillRuns()
    {
        SkipWithoutDocker();
        var request = new SandboxRequest(Image,
            [File("Loop.java", "public class Loop { public static void main(String[] a) { while (true) { } } }")],
            [
                new SandboxStep("compile", ["javac", "Loop.java"], StopOnFailure: true),
                new SandboxStep("run", ["java", "Loop"], Timeout: TimeSpan.FromSeconds(2)),
                new SandboxStep("after", ["echo", "still here"]),
            ]);

        var watch = Stopwatch.StartNew();
        var result = await Runner().RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(SandboxStepOutcome.TimedOut, result.Steps[1].Outcome);
        Assert.Null(result.Steps[1].ExitCode);
        Assert.Equal((SandboxStepOutcome.Succeeded, "still here\n"), (result.Steps[2].Outcome, result.Steps[2].Stdout));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task EndlessOutput_IsCutOffAtTheLimit()
    {
        SkipWithoutDocker();
        var request = new SandboxRequest(Image, [],
            [new SandboxStep("spam", ["sh", "-c", "yes 'line of output' | head -c 200000"])],
            new SandboxLimits(MaxOutputBytes: 1000));

        var result = await Runner().RunAsync(request, TestContext.Current.CancellationToken);

        var step = Assert.Single(result.Steps);
        Assert.Equal(SandboxStepOutcome.Succeeded, step.Outcome);
        Assert.True(step.OutputTruncated);
        Assert.Equal(1000, Encoding.UTF8.GetByteCount(step.Stdout));
    }

    [Fact]
    public async Task TheContainerIsLockedDown_AndRemovedAfterwards()
    {
        SkipWithoutDocker();
        var request = new SandboxRequest(Image, [File("note.txt", "hi")],
            [
                new SandboxStep("user", ["id", "-u"]),
                new SandboxStep("network", ["getent", "hosts", "example.com"]),
                new SandboxStep("write outside work", ["touch", "/etc/x"]),
                new SandboxStep("read work", ["cat", "note.txt"]),
            ]);

        var result = await Runner().RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("10001\n", result.Steps[0].Stdout);
        Assert.Equal(SandboxStepOutcome.Failed, result.Steps[1].Outcome);
        Assert.Contains("Read-only file system", result.Steps[2].Stderr);
        Assert.Equal("hi", result.Steps[3].Stdout);
        Assert.Equal("", Docker("ps", "--all", "--quiet", "--filter", "label=teaching-assistant.sandbox=true").Output.Trim());
    }

    [Fact]
    public async Task AMissingImage_IsReportedAsUnavailable()
    {
        SkipWithoutDocker();
        var request = new SandboxRequest("ta-java:does-not-exist", [], [new SandboxStep("x", ["true"])]);

        var error = await Assert.ThrowsAsync<SandboxUnavailableException>(() =>
            Runner().RunAsync(request, TestContext.Current.CancellationToken));

        Assert.Contains("is not built on this machine", error.Message);
    }

    [Fact]
    public async Task AMissingDockerProgram_IsReportedAsUnavailable()
    {
        var request = new SandboxRequest(Image, [], [new SandboxStep("x", ["true"])]);

        await Assert.ThrowsAsync<SandboxUnavailableException>(() =>
            Runner(new SandboxOptions { DockerPath = "no-such-docker-program" }).RunAsync(request, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("src/Main.java", "src/Main.java")]
    [InlineData(@"src\shop\Main.java", "src/shop/Main.java")]
    public void NormalizePath_KeepsRelativePaths(string path, string expected)
    {
        Assert.Equal(expected, DockerSandboxRunner.NormalizePath(path));
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("../outside.java")]
    [InlineData("src/../../outside.java")]
    [InlineData("C:/Windows/x")]
    [InlineData("src//Main.java")]
    [InlineData("")]
    public void NormalizePath_RefusesAWayOutOfTheWorkingFolder(string path)
    {
        Assert.Throws<ArgumentException>(() => DockerSandboxRunner.NormalizePath(path));
    }

    private static string? CheckDocker()
    {
        try
        {
            var (exitCode, _) = Docker("image", "inspect", "--format", "{{.Id}}", Image);
            return exitCode == 0 ? null : $"The {Image} image is not built (see sandbox/README.md).";
        }
        catch (Exception ex)
        {
            return $"Docker is not available: {ex.Message}";
        }
    }

    private static (int ExitCode, string Output) Docker(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);
        return (process.ExitCode, output);
    }
}
