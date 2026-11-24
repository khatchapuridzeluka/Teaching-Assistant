using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Services;

public class TestDesignService : ITestDesignService
{
    internal const int MaxReferenceFiles = 30;
    internal const int MaxReferenceCharacters = 200_000;
    internal const int MaxNoteCharacters = 2_000;
    internal const int MaxCases = 200;
    internal const int MaxFieldCharacters = 10_000;

    private const string Schema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "requirements": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "id": { "type": "string" },
                  "text": { "type": "string" },
                  "testable": { "type": "boolean" },
                  "reason": { "type": "string" }
                },
                "required": ["id", "text", "testable", "reason"]
              }
            },
            "interface": { "type": "string" },
            "cases": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "title": { "type": "string" },
                  "category": { "type": "string", "enum": ["Normal", "Boundary", "Invalid", "LargeInput", "Concurrency"] },
                  "requirementIds": { "type": "array", "items": { "type": "string" } },
                  "steps": { "type": "string" },
                  "expected": { "type": "string" }
                },
                "required": ["title", "category", "requirementIds", "steps", "expected"]
              }
            }
          },
          "required": ["requirements", "interface", "cases"]
        }
        """;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ITestSetStore _testSets;
    private readonly IGradingService _grading;
    private readonly ICourseGuidanceStore _courseGuidance;
    private readonly IPolicyRuleStore _rules;
    private readonly IAssignmentNoteStore _notes;
    private readonly ILanguageModel _languageModel;
    private readonly ModelCallContext _callContext;
    private readonly IJobService _jobs;
    private readonly TimeProvider _timeProvider;

    public TestDesignService(
        ITestSetStore testSets, IGradingService grading, ICourseGuidanceStore courseGuidance, IPolicyRuleStore rules,
        IAssignmentNoteStore notes, ILanguageModel languageModel, ModelCallContext callContext, IJobService jobs,
        TimeProvider timeProvider)
    {
        _jobs = jobs;
        _testSets = testSets;
        _grading = grading;
        _courseGuidance = courseGuidance;
        _rules = rules;
        _notes = notes;
        _languageModel = languageModel;
        _callContext = callContext;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> GenerateAsync(
        string courseId, string assignmentId, GenerateTestsRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var instructions = await _grading.BuildInstructionsAsync(assignmentId, cancellationToken)
                           ?? throw new InvalidRequestException(
                               "The assignment's instructions are not stored or cannot be read. Fetch the assignment first.");

        var guidance = await _courseGuidance.FindAsync(courseId, cancellationToken);
        var rules = await _rules.GetActiveAsync(cancellationToken);
        var notes = await _notes.GetActiveAsync(assignmentId, cancellationToken);
        var policy = GradingPromptSections.Build([], guidance?.Text, rules, notes);

        _callContext.DescribeAssignment(courseId, assignmentId);
        var parts = instructions.Append(new TextPart { Text = Prompt(request, policy) }).ToList();
        var reply = await _languageModel.DesignTestsAsync(PromptLibrary.Tests, parts, Schema, cancellationToken);
        var content = Read(reply.Text, request);

        var latest = await _testSets.GetLatestAsync(assignmentId, cancellationToken);
        var testSet = new TestSet(courseId, assignmentId, (latest?.Version ?? 0) + 1, request.Style,
            JsonSerializer.Serialize(request, JsonOptions), JsonSerializer.Serialize(content, JsonOptions), reply.Model,
            _timeProvider.GetUtcNow());
        _testSets.Add(testSet);
        await _testSets.SaveChangesAsync(cancellationToken);
        return testSet.Id;
    }

    public async Task<TestSetResponse?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        var testSet = await _testSets.GetLatestAsync(assignmentId, cancellationToken);
        return testSet is null ? null : await RespondAsync(testSet, cancellationToken);
    }

    public async Task<TestSetResponse?> SaveAsync(
        string assignmentId, Guid testSetId, SaveTestSetRequest request, CancellationToken cancellationToken = default)
    {
        var testSet = await FindLatestAsync(assignmentId, testSetId, cancellationToken);
        if (testSet is null)
        {
            return null;
        }

        var content = JsonSerializer.Serialize(Normalize(request, testSet.Style), JsonOptions);
        if (testSet.Status == TestSetStatus.Draft)
        {
            testSet.Edit(content);
        }
        else
        {
            testSet = new TestSet(testSet.CourseId, testSet.AssignmentId, testSet.Version + 1, testSet.Style, testSet.Request, content,
                testSet.Model, _timeProvider.GetUtcNow());
            _testSets.Add(testSet);
        }

        await _testSets.SaveChangesAsync(cancellationToken);
        return await RespondAsync(testSet, cancellationToken);
    }

    public async Task<JobResponse?> QueueCodeAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default)
    {
        var testSet = await FindLatestAsync(assignmentId, testSetId, cancellationToken);
        if (testSet is null)
        {
            return null;
        }

        TestCodeService.EnsureCanHaveCode(testSet);
        return await _jobs.QueueGenerateTestCodeAsync(testSet.CourseId, testSet.AssignmentId, testSet.Id, cancellationToken);
    }

    public async Task<TestSetResponse?> ApproveAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default)
    {
        var testSet = await FindLatestAsync(assignmentId, testSetId, cancellationToken);
        if (testSet is null)
        {
            return null;
        }

        if (testSet.Status == TestSetStatus.Draft)
        {
            testSet.Approve();
            await _testSets.SaveChangesAsync(cancellationToken);
            if (testSet.Style == TestStyle.ClassDesign)
            {
                await _jobs.QueueGenerateTestCodeAsync(testSet.CourseId, testSet.AssignmentId, testSet.Id, cancellationToken);
            }
        }

        return await RespondAsync(testSet, cancellationToken);
    }

    public static void Validate(GenerateTestsRequest request)
    {
        if (request.Categories.Count == 0)
        {
            throw new InvalidRequestException("Choose at least one kind of test case to cover.");
        }

        if (request.Focus?.Length > MaxNoteCharacters || request.DontTest?.Length > MaxNoteCharacters)
        {
            throw new InvalidRequestException($"Keep the focus and the \"don't test\" notes under {MaxNoteCharacters} characters each.");
        }

        if (request.ReferenceFiles.Count > MaxReferenceFiles)
        {
            throw new InvalidRequestException($"A reference solution can have at most {MaxReferenceFiles} files.");
        }

        if (request.ReferenceFiles.FirstOrDefault(f => !f.Name.EndsWith(".java", StringComparison.OrdinalIgnoreCase)) is { } other)
        {
            throw new InvalidRequestException($"The reference solution takes .java files only; \"{other.Name}\" is not one.");
        }

        if (request.ReferenceFiles.Sum(f => f.Content.Length) > MaxReferenceCharacters)
        {
            throw new InvalidRequestException("The reference solution is too large; send only the .java files the tests need.");
        }
    }

    internal static TestSetContent Normalize(SaveTestSetRequest request, TestStyle style)
    {
        if (request.Cases.Count == 0)
        {
            throw new InvalidRequestException("A test set needs at least one case.");
        }

        if (request.Cases.Count > MaxCases || request.Requirements.Count > MaxCases)
        {
            throw new InvalidRequestException($"A test set can have at most {MaxCases} cases and {MaxCases} requirements.");
        }

        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var requirements = new List<TestRequirement>();
        foreach (var requirement in request.Requirements)
        {
            if (string.IsNullOrWhiteSpace(requirement.Text))
            {
                throw new InvalidRequestException($"Requirement {requirements.Count + 1} has no text. Write it or delete it.");
            }

            var id = $"R{requirements.Count + 1}";
            ids.TryAdd(requirement.Id.Trim(), id);
            requirements.Add(new TestRequirement
            {
                Id = id,
                Text = Limit(requirement.Text),
                Testable = requirement.Testable,
                Reason = requirement.Testable || string.IsNullOrWhiteSpace(requirement.Reason) ? null : Limit(requirement.Reason),
            });
        }

        var cases = new List<TestCase>();
        foreach (var testCase in request.Cases)
        {
            var number = cases.Count + 1;
            if (string.IsNullOrWhiteSpace(testCase.Title) || string.IsNullOrWhiteSpace(testCase.Steps))
            {
                throw new InvalidRequestException($"Case {number} needs a title and steps.");
            }

            cases.Add(new TestCase
            {
                Id = $"T{number}",
                Title = Limit(testCase.Title),
                Category = testCase.Category,
                RequirementIds = testCase.RequirementIds.Select(r => ids.GetValueOrDefault(r.Trim())).OfType<string>().Distinct().ToList(),
                Steps = Limit(testCase.Steps),
                Expected = string.IsNullOrWhiteSpace(testCase.Expected) ? null : Limit(testCase.Expected),
            });
        }

        var contract = style == TestStyle.ClassDesign && !string.IsNullOrWhiteSpace(request.Interface) ? Limit(request.Interface) : null;
        return new TestSetContent { Requirements = requirements, Interface = contract, Cases = cases };
    }

    internal static string Prompt(GenerateTestsRequest request, string policy)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("<test_request>");
        prompt.Append("Style: ").AppendLine(request.Style == TestStyle.Console
            ? "console (the program reads standard input and prints its results)"
            : "class design (tests call a neutral interface; each student's classes are connected to it by an adapter)");
        prompt.Append("Cover: ").AppendLine(string.Join(", ", request.Categories.Distinct().Select(Describe)));
        prompt.Append("How many: ").AppendLine(request.Amount switch
        {
            TestAmount.AboutThreePerCategory => "about 3 cases for each kind above",
            TestAmount.AboutTenInTotal => "about 10 cases in total",
            _ => "as few cases as it takes to check every testable requirement at least once",
        });
        if (!string.IsNullOrWhiteSpace(request.Focus))
        {
            prompt.Append("Focus: ").AppendLine(request.Focus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.DontTest))
        {
            prompt.Append("Don't test: ").AppendLine(request.DontTest.Trim());
        }

        prompt.AppendLine("</test_request>");

        if (!string.IsNullOrWhiteSpace(policy))
        {
            prompt.AppendLine().AppendLine("<grading_policy>").AppendLine(policy).AppendLine("</grading_policy>");
        }

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

        prompt.AppendLine().Append("Design the test cases for this assignment.");
        return prompt.ToString().ReplaceLineEndings("\n");
    }

    internal static TestSetContent Read(string reply, GenerateTestsRequest request)
    {
        Reply answer;
        try
        {
            answer = JsonSerializer.Deserialize<Reply>(reply, JsonOptions)
                     ?? throw new LanguageModelResponseException("The test design came back empty.");
        }
        catch (JsonException ex)
        {
            throw new LanguageModelResponseException("The test design answer could not be read.", ex);
        }

        var requirementIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var requirements = new List<TestRequirement>();
        foreach (var item in answer.Requirements ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Text))
            {
                continue;
            }

            var id = $"R{requirements.Count + 1}";
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                requirementIds.TryAdd(item.Id.Trim(), id);
            }

            requirements.Add(new TestRequirement
            {
                Id = id,
                Text = item.Text.Trim(),
                Testable = item.Testable,
                Reason = item.Testable || string.IsNullOrWhiteSpace(item.Reason) ? null : item.Reason.Trim(),
            });
        }

        var cases = new List<TestCase>();
        foreach (var item in answer.Cases ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Title) || item.Steps is null || item.Category is not { } category)
            {
                continue;
            }

            cases.Add(new TestCase
            {
                Id = $"T{cases.Count + 1}",
                Title = item.Title.Trim(),
                Category = category,
                RequirementIds = (item.RequirementIds ?? [])
                    .Select(r => requirementIds.GetValueOrDefault(r.Trim()))
                    .OfType<string>()
                    .Distinct()
                    .ToList(),
                Steps = item.Steps.Trim('\n', '\r'),
                Expected = string.IsNullOrWhiteSpace(item.Expected) ? null : item.Expected.Trim(),
            });
        }

        if (cases.Count == 0)
        {
            throw new LanguageModelResponseException("The model proposed no test cases. Check the instructions and try again.");
        }

        var contract = request.Style == TestStyle.ClassDesign && !string.IsNullOrWhiteSpace(answer.Interface)
            ? answer.Interface.Trim()
            : null;
        return new TestSetContent { Requirements = requirements, Interface = contract, Cases = cases };
    }

    internal static TestSetContent ReadContent(TestSet testSet)
    {
        return JsonSerializer.Deserialize<TestSetContent>(testSet.Content, JsonOptions)
               ?? throw new InvalidOperationException($"Test set {testSet.Id} has no content.");
    }

    internal static GenerateTestsRequest ReadRequest(TestSet testSet)
    {
        return JsonSerializer.Deserialize<GenerateTestsRequest>(testSet.Request, JsonOptions)
               ?? throw new InvalidOperationException($"Test set {testSet.Id} has no request.");
    }

    internal static TestSetResponse ToResponse(TestSet testSet, int? approvedVersion)
    {
        var content = ReadContent(testSet);
        var request = ReadRequest(testSet);

        return new TestSetResponse
        {
            Id = testSet.Id,
            Version = testSet.Version,
            Status = testSet.Status,
            Style = testSet.Style,
            Request = request,
            Requirements = content.Requirements
                .Select(r => r with { TestCount = content.Cases.Count(c => c.RequirementIds.Contains(r.Id)) })
                .ToList(),
            Interface = content.Interface,
            Cases = content.Cases,
            ApprovedVersion = approvedVersion,
            Code = content.Code,
            Model = testSet.Model,
            CreatedAt = testSet.CreatedAt,
        };
    }

    private static string Limit(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > MaxFieldCharacters)
        {
            throw new InvalidRequestException($"Keep each text under {MaxFieldCharacters} characters.");
        }

        return trimmed;
    }

    private async Task<TestSet?> FindLatestAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken)
    {
        var testSet = await _testSets.FindAsync(testSetId, cancellationToken);
        if (testSet is null || testSet.AssignmentId != assignmentId)
        {
            return null;
        }

        var latest = await _testSets.GetLatestAsync(assignmentId, cancellationToken);
        if (latest is not null && latest.Id != testSet.Id)
        {
            throw new InvalidRequestException(
                $"Version {latest.Version} is newer than version {testSet.Version}. Reload the page and work on the newest version.");
        }

        return testSet;
    }

    private async Task<TestSetResponse> RespondAsync(TestSet testSet, CancellationToken cancellationToken)
    {
        var approved = await _testSets.GetLatestApprovedAsync(testSet.AssignmentId, cancellationToken);
        return ToResponse(testSet, approved?.Version);
    }

    private static string Describe(TestCategory category) => category switch
    {
        TestCategory.Normal => "normal cases",
        TestCategory.Boundary => "boundaries (0, empty, one element, the largest allowed value)",
        TestCategory.Invalid => "invalid input and the exceptions or refusals the instructions require",
        TestCategory.LargeInput => "large inputs",
        _ => "concurrency",
    };

    private sealed record Reply
    {
        public List<ReplyRequirement>? Requirements { get; init; }
        public string? Interface { get; init; }
        public List<ReplyCase>? Cases { get; init; }
    }

    private sealed record ReplyRequirement
    {
        public string? Id { get; init; }
        public string? Text { get; init; }
        public bool Testable { get; init; }
        public string? Reason { get; init; }
    }

    private sealed record ReplyCase
    {
        public string? Title { get; init; }
        public TestCategory? Category { get; init; }
        public List<string>? RequirementIds { get; init; }
        public string? Steps { get; init; }
        public string? Expected { get; init; }
    }
}
