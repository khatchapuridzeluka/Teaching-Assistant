using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Infrastructure.Sandbox;
using TeachingAssistant.Tests.Application;

namespace TeachingAssistant.Tests.Infrastructure;

/// <summary>The compile check end to end against the real Docker sandbox; skipped without Docker or the ta-java:27 image.</summary>
[Collection(DockerCollection.Name)]
public class JavaBuildCheckDockerTests
{
    private readonly FakeCodeStorage _storage = new();

    private JavaBuildCheck Check() =>
        new(new DockerSandboxRunner(Options.Create(new SandboxOptions()), NullLogger<DockerSandboxRunner>.Instance), _storage);

    private static SubmissionVersion Version()
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", "assignment-1", "student-1", "Student",
            "submission-1", "folder", DateTimeOffset.UtcNow);
        return submission.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, DateTimeOffset.UtcNow, []);
    }

    [Fact]
    public async Task TwoTasksWithTheirOwnMain_AndAByteOrderMark_Compile()
    {
        SkipWithoutDocker();
        _storage.Files["task1/Main.java"] = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("public class Main { public static void main(String[] a) { } }")];
        _storage.Files["task2/Main.java"] = Encoding.UTF8.GetBytes("public class Main { public static void main(String[] a) { } }");
        _storage.Files["shop/src/ge/kiu/Shop.java"] = Encoding.UTF8.GetBytes("package ge.kiu;\npublic class Shop { Item item = new Item(); }");
        _storage.Files["shop/src/ge/kiu/Item.java"] = Encoding.UTF8.GetBytes("package ge.kiu;\npublic class Item { }");

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal((BuildStatus.Compiled, 3, ""), (outcome.Status, outcome.Programs, outcome.Output));
    }

    [Fact]
    public async Task ACompileError_ComesBackWithTheStudentsPathAndLine()
    {
        SkipWithoutDocker();
        _storage.Files["src/Zoo.java"] = Encoding.UTF8.GetBytes("public class Zoo {\n    int count = \"many\";\n}");

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal(BuildStatus.CompileFailed, outcome.Status);
        Assert.StartsWith("src/Zoo.java:2: error: incompatible types: String cannot be converted to int", outcome.Output);
    }

    [Fact]
    public async Task BeginnerWarnings_AreReported_AndTheCodeStillCompiles()
    {
        SkipWithoutDocker();
        _storage.Files["Shapes.java"] = Encoding.UTF8.GetBytes("""
            import java.util.ArrayList;
            import java.util.List;

            public class Shapes {
                static int count;

                @SuppressWarnings("unused")
                void all(int kind, Shapes other) {
                    List names = new ArrayList();
                    names.add("square");
                    Integer boxed = new Integer(5);
                    switch (kind) {
                        case 1:
                            count++;
                        case 2:
                            count--;
                            break;
                        default:
                            break;
                    }
                    int same = other.count;
                    String text = (String) "already a string";
                    int broken = 1 / 0;
                    if (kind > 3);
                }

                @Override
                public boolean equals(Object o) {
                    return o == this;
                }
            }
            """);

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal(BuildStatus.Compiled, outcome.Status);
        var missing = new[] { "[rawtypes]", "[unchecked]", "[deprecation]", "[fallthrough]", "[static]", "[cast]", "[divzero]", "[empty]", "[overrides]" }
            .Where(kind => !outcome.Output.Contains(kind)).ToList();
        Assert.True(missing.Count == 0, $"missing {string.Join(", ", missing)} in:\n{outcome.Output}");

        Assert.True(outcome.Warnings >= 9, $"{outcome.Warnings} warnings:\n{outcome.Output}");
    }

    [Fact]
    public async Task Checkstyle_ReportsTheShortListsFindings_WithTheSubmissionsPaths()
    {
        SkipWithoutDocker();
        _storage.Files["src/shop/Main.java"] = Encoding.UTF8.GetBytes("""
            package shop;

            import java.util.List;

            public class Main {
                public int Count;

                public static void main(String[] args) {
                    String name = args.length > 0 ? args[0] : "";
                    if (name == "admin") System.out.println("hi");
                }
            }
            """);

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal(BuildStatus.Compiled, outcome.Status);
        Assert.Equal(5, outcome.StyleFindings);
        foreach (var rule in new[] { "[UnusedImports]", "[VisibilityModifier]", "[MemberName]", "[NeedBraces]", "[StringLiteralEquality]" })
        {
            Assert.Contains(rule, outcome.StyleOutput);
        }

        Assert.StartsWith("src/shop/Main.java:", outcome.StyleOutput);
    }

    [Fact]
    public async Task Pmd_ReportsTheShortListsFindings_WithTheSubmissionsPaths()
    {
        SkipWithoutDocker();
        _storage.Files["src/shop/Main.java"] = Encoding.UTF8.GetBytes("""
            package shop;

            import java.util.Scanner;

            public class Main {
                public static void main(String[] args) {
                    Scanner in = new Scanner(System.in);
                    Integer a = in.nextInt();
                    Integer b = in.nextInt();
                    if (a == b) {
                        System.out.println("same");
                    }
                    String text = "";
                    for (int i = 0; i < 3; i++) {
                        text += i;
                    }
                    System.out.println(text);
                }
            }
            """);

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal(BuildStatus.Compiled, outcome.Status);
        foreach (var rule in new[] { "[CloseResource]", "[CompareObjectsWithEquals]", "[UseStringBufferForStringAppends]" })
        {
            Assert.Contains(rule, outcome.PmdOutput);
        }

        Assert.Equal(3, outcome.PmdFindings);
        Assert.StartsWith("src/shop/Main.java:", outcome.PmdOutput);
    }

    private static void SkipWithoutDocker()
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "image", "inspect", "--format", "{{.Id}}", JavaBuildCheck.Image })
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start)!;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);
            Assert.SkipWhen(process.ExitCode != 0, $"The {JavaBuildCheck.Image} image is not built (see sandbox/README.md).");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Skip("Docker is not installed.");
        }
    }
}
