using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Services;

public partial class TestCodeService : ITestCodeService
{
    public const string TestClassFile = "AssignmentTests.java";
    public const string AdapterFile = "StudentAdapter.java";
    public const string JUnitJar = "/opt/junit/junit.jar";

    private const string Schema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "testClass": { "type": "string" },
            "stubAdapter": { "type": "string" }
          },
          "required": ["testClass", "stubAdapter"]
        }
        """;

    private const int MaxCompileOutputChars = 6_000;
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(90);

    private readonly ITestSetStore _testSets;
    private readonly ILanguageModel _languageModel;
    private readonly ModelCallContext _callContext;
    private readonly ISandboxRunner _sandbox;
    private readonly TimeProvider _timeProvider;

    public TestCodeService(
        ITestSetStore testSets, ILanguageModel languageModel, ModelCallContext callContext, ISandboxRunner sandbox,
        TimeProvider timeProvider)
    {
        _testSets = testSets;
        _languageModel = languageModel;
        _callContext = callContext;
        _sandbox = sandbox;
        _timeProvider = timeProvider;
    }

    public async Task GenerateAsync(Guid testSetId, CancellationToken cancellationToken = default)
    {
        var testSet = await _testSets.FindAsync(testSetId, cancellationToken)
                      ?? throw new InvalidRequestException("The test set no longer exists.");
        EnsureCanHaveCode(testSet);

        var content = TestDesignService.ReadContent(testSet);
        var interfaceName = InterfaceName(content.Interface)
                            ?? throw new InvalidRequestException(
                                "The test set has no Java interface to write the tests against. Add it on the Tests view.");

        _callContext.DescribeAssignment(testSet.CourseId, testSet.AssignmentId);
        var prompt = Prompt(content, interfaceName, TestDesignService.ReadRequest(testSet));
        var reply = await _languageModel.DesignTestsAsync(PromptLibrary.TestCode, [new TextPart { Text = prompt }], Schema,
            cancellationToken);
        var (testClass, stubAdapter) = Read(reply.Text);

        var interfaceFile = interfaceName + ".java";
        var (status, output) = await CompileAsync(interfaceFile, content.Interface!, stubAdapter, testClass, cancellationToken);
        var code = new TestCode
        {
            InterfaceFile = interfaceFile,
            TestClass = testClass,
            StubAdapter = stubAdapter,
            Status = status,
            CompileOutput = output,
            Model = reply.Model,
            CreatedAt = _timeProvider.GetUtcNow(),
        };
        testSet.RecordCode(JsonSerializer.Serialize(content with { Code = code }, TestDesignService.JsonOptions));
        await _testSets.SaveChangesAsync(cancellationToken);
    }

    public static void EnsureCanHaveCode(TestSet testSet)
    {
        if (testSet.Status != TestSetStatus.Approved)
        {
            throw new InvalidRequestException("Approve the test set before its test code is written.");
        }

        if (testSet.Style != TestStyle.ClassDesign)
        {
            throw new InvalidRequestException("Console tests run the student's program directly and need no test code.");
        }
    }

    internal static string? InterfaceName(string? source)
    {
        return source is null ? null : InterfacePattern().Match(source) is { Success: true } match ? match.Groups[1].Value : null;
    }

    internal static string Prompt(TestSetContent content, string interfaceName, GenerateTestsRequest request)
    {
        var prompt = new StringBuilder();
        prompt.Append("<interface file=\"").Append(interfaceName).AppendLine(".java\">");
        prompt.AppendLine(content.Interface!.TrimEnd());
        prompt.AppendLine("</interface>").AppendLine();

        var requirements = content.Requirements.ToDictionary(r => r.Id, r => r.Text);
        prompt.AppendLine("<test_cases>");
        foreach (var testCase in content.Cases)
        {
            prompt.Append("<case id=\"").Append(testCase.Id).Append("\" title=\"").Append(testCase.Title).AppendLine("\">");
            if (testCase.RequirementIds.Count > 0)
            {
                prompt.Append("Checks: ").AppendLine(string.Join("; ",
                    testCase.RequirementIds.Select(id => requirements.TryGetValue(id, out var text) ? $"{id} {text}" : id)));
            }

            prompt.AppendLine("Steps:").AppendLine(testCase.Steps.TrimEnd());
            prompt.AppendLine("Expected:").AppendLine(testCase.Expected?.TrimEnd()
                                                      ?? "(not written; check only that the steps run without an exception)");
            prompt.AppendLine("</case>");
        }

        prompt.AppendLine("</test_cases>");

        if (request.ReferenceFiles.Count > 0)
        {
            prompt.AppendLine().AppendLine("<reference_solution>");
            foreach (var file in request.ReferenceFiles)
            {
                prompt.Append("<file name=\"").Append(file.Name).AppendLine("\">");
                prompt.AppendLine(file.Content.TrimEnd());
                prompt.AppendLine("</file>");
            }

            prompt.AppendLine("</reference_solution>");
        }

        prompt.AppendLine().Append("Write the test class and the placeholder adapter.");
        return prompt.ToString().ReplaceLineEndings("\n");
    }

    internal static (string TestClass, string StubAdapter) Read(string reply)
    {
        Reply answer;
        try
        {
            answer = JsonSerializer.Deserialize<Reply>(reply, TestDesignService.JsonOptions)
                     ?? throw new LanguageModelResponseException("The test code came back empty.");
        }
        catch (JsonException ex)
        {
            throw new LanguageModelResponseException("The test code answer could not be read.", ex);
        }

        if (string.IsNullOrWhiteSpace(answer.TestClass) || !answer.TestClass.Contains("class AssignmentTests", StringComparison.Ordinal))
        {
            throw new LanguageModelResponseException("The answer has no AssignmentTests class.");
        }

        if (string.IsNullOrWhiteSpace(answer.StubAdapter) || !answer.StubAdapter.Contains("class StudentAdapter", StringComparison.Ordinal))
        {
            throw new LanguageModelResponseException("The answer has no StudentAdapter placeholder.");
        }

        return (answer.TestClass.Trim() + "\n", answer.StubAdapter.Trim() + "\n");
    }

    private async Task<(TestCodeStatus Status, string? Output)> CompileAsync(
        string interfaceFile, string interfaceSource, string stubAdapter, string testClass, CancellationToken cancellationToken)
    {
        var utf8 = new UTF8Encoding(false);
        var request = new SandboxRequest
        {
            Image = JavaBuildCheck.Image,
            Files =
            [
                new SandboxFile { Path = interfaceFile, Content = utf8.GetBytes(interfaceSource.TrimEnd() + "\n") },
                new SandboxFile { Path = AdapterFile, Content = utf8.GetBytes(stubAdapter) },
                new SandboxFile { Path = TestClassFile, Content = utf8.GetBytes(testClass) },
            ],
            Steps =
            [
                new SandboxStep
                {
                    Name = "compile tests",
                    Command = ["javac", "-encoding", "UTF-8", "-d", "out", "-cp", JUnitJar, interfaceFile, AdapterFile, TestClassFile],
                    Timeout = CompileTimeout,
                },
            ],
        };

        try
        {
            var step = (await _sandbox.RunAsync(request, cancellationToken)).Steps.Single();
            if (step.Outcome == SandboxStepOutcome.Succeeded)
            {
                return (TestCodeStatus.Compiled, null);
            }

            var output = (step.Stderr + step.Stdout).Trim();
            if (output.Contains("package org.junit.jupiter.api does not exist", StringComparison.Ordinal))
            {
                return (TestCodeStatus.NotChecked,
                    "The sandbox image has no JUnit yet. Rebuild it as described in sandbox/README.md, then generate the code again.");
            }

            if (step.Outcome == SandboxStepOutcome.TimedOut)
            {
                output = "Compiling did not finish in time. " + output;
            }

            return (TestCodeStatus.CompileFailed, output.Length > MaxCompileOutputChars ? output[..MaxCompileOutputChars] + "\n…" : output);
        }
        catch (SandboxUnavailableException ex)
        {
            return (TestCodeStatus.NotChecked, ex.Message);
        }
    }

    [GeneratedRegex(@"\binterface\s+([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex InterfacePattern();

    private sealed record Reply
    {
        public string? TestClass { get; init; }
        public string? StubAdapter { get; init; }
    }
}
