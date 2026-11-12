using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class StudentAnalysisServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Fakes _fakes = new();
    private readonly ModelCallContext _context = new();

    private StudentAnalysisService Service() => new(_fakes, _fakes, _fakes, _fakes, _fakes, _context, TimeProvider.System);

    [Fact]
    public async Task MistakesAnalysis_SendsOnlyMistakesWithPointsAndLabels_NeverTheNameOrStrengths()
    {
        _fakes.Assignment("a1", "Assignment 1", Now.AddDays(-20));
        _fakes.Assignment("a2", "Assignment 2", Now.AddDays(-10));
        _fakes.Approve("a1", grade: 70, mistakes: [MistakeCategory.ErrorHandling, MistakeCategory.Naming], strengths: [MistakeCategory.Design]);
        _fakes.Approve("a2", grade: 92, mistakes: [MistakeCategory.Naming], strengths: [MistakeCategory.Design]);
        _fakes.Reply = """{ "summary": " Naming keeps costing points. ", "nextSteps": "Practise naming." }""";

        await Service().AnalyzeAsync("course", "student", StudentAnalysisKind.Mistakes, TestContext.Current.CancellationToken);

        var prompt = _fakes.Prompt!;
        Assert.DoesNotContain("Ana Beridze", prompt);
        Assert.DoesNotContain("done well", prompt);
        Assert.Contains("<assignment title=\"Assignment 1\" due=", prompt);
        Assert.Contains("points=\"100\" lost=\"4\"", prompt);
        Assert.Contains("Minor · Naming · -2 pts · repeated (first in Assignment 1)", prompt);
        Assert.Contains("Naming: 2 in 2 assignments, repeated, 4 points lost", prompt);
        Assert.True(_context.StudentAnalysis);

        var saved = Assert.Single(_fakes.Analyses);
        Assert.Equal((StudentAnalysisKind.Mistakes, "Naming keeps costing points.", "Practise naming.", 2, 3),
            (saved.Kind, saved.Summary, saved.NextSteps, saved.Assignments, saved.Records));
        Assert.False((await Service().GetLatestAsync("course", "student", StudentAnalysisKind.Mistakes, TestContext.Current.CancellationToken))!.Outdated);
        Assert.Null(await Service().GetLatestAsync("course", "student", StudentAnalysisKind.Strengths, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StrengthsAnalysis_SendsOnlyNumberedStrengths_AndKeepsEveryStrengthInExactlyOneGroup()
    {
        _fakes.Assignment("a1", "Assignment 1", Now.AddDays(-20));
        _fakes.Assignment("a2", "Assignment 2", Now.AddDays(-10));
        _fakes.Approve("a1", grade: 70, mistakes: [MistakeCategory.ErrorHandling], strengths: [MistakeCategory.Design]);
        _fakes.Approve("a2", grade: 92, mistakes: [], strengths: [MistakeCategory.Design, MistakeCategory.ErrorHandling, MistakeCategory.Naming]);
        _fakes.Reply = """
            { "overview": "Strong in design.",
              "groups": [
                { "title": "Clean class design", "explanation": "Keeps classes focused.", "strengthIds": [1, 2, 99] },
                { "title": "Duplicate", "explanation": "x", "strengthIds": [2] },
                { "title": "Checks its inputs", "explanation": "Validates before use.", "strengthIds": [3] }
              ] }
            """;

        await Service().AnalyzeAsync("course", "student", StudentAnalysisKind.Strengths, TestContext.Current.CancellationToken);

        var prompt = _fakes.Prompt!;
        Assert.DoesNotContain("Ana Beridze", prompt);
        Assert.DoesNotContain("slip", prompt);
        Assert.Contains("[1] Assignment 1 · Design · new · Design done well", prompt);
        Assert.Contains("[2] Assignment 2 · Design · consistent (also in Assignment 1)", prompt);
        Assert.Contains("ErrorHandling · improved (was a mistake in Assignment 1)", prompt);

        var latest = (await Service().GetLatestAsync("course", "student", StudentAnalysisKind.Strengths, TestContext.Current.CancellationToken))!;
        Assert.Equal((StudentAnalysisKind.Strengths, "Strong in design.", 4), (latest.Kind, latest.Summary, latest.Records));
        Assert.Equal(["Clean class design", "Checks its inputs", "Not grouped"], latest.Groups.Select(g => g.Title));
        Assert.Equal((2, 2, false), (latest.Groups[0].Count, latest.Groups[0].Assignments, latest.Groups[0].Ungrouped));
        Assert.True(latest.Groups[2].Ungrouped);
        Assert.Equal(4, latest.Groups.Sum(g => g.Count));
    }

    [Fact]
    public async Task TheAnalysis_IsOutdated_OnceAnotherReportAddsItsKindOfRecord()
    {
        _fakes.Assignment("a1", "Assignment 1", Now.AddDays(-20));
        _fakes.Approve("a1", grade: 70, mistakes: [MistakeCategory.Naming], strengths: []);
        _fakes.Reply = """{ "summary": "s", "nextSteps": "n" }""";
        await Service().AnalyzeAsync("course", "student", StudentAnalysisKind.Mistakes, TestContext.Current.CancellationToken);

        _fakes.Approve("a2", grade: 80, mistakes: [MistakeCategory.Design], strengths: []);

        Assert.True((await Service().GetLatestAsync("course", "student", StudentAnalysisKind.Mistakes, TestContext.Current.CancellationToken))!.Outdated);
    }

    [Fact]
    public async Task Analyze_WithoutRecordsOfThatKind_IsRejected_AndNothingIsAsked()
    {
        _fakes.Approve("a1", grade: 70, mistakes: [MistakeCategory.Naming], strengths: []);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().AnalyzeAsync("course", "student", StudentAnalysisKind.Strengths, TestContext.Current.CancellationToken));
        Assert.Null(_fakes.Prompt);
    }

    private sealed class Fakes : IStudentMistakeStore, IStudentStrengthStore, ICourseAssignmentStore, ISubmissionStore, IGradingReportStore,
        IStudentAnalysisStore, ILanguageModel
    {
        private readonly List<StudentMistake> _mistakes = [];
        private readonly List<StudentStrength> _strengths = [];
        private readonly List<CourseAssignment> _assignments = [];
        private readonly List<Submission> _submissions = [];
        private int _approvals;

        public List<StudentAnalysis> Analyses { get; } = [];
        public string Reply { get; set; } = "{}";
        public string? Prompt { get; private set; }

        public void Assignment(string id, string title, DateTimeOffset dueAt) =>
            _assignments.Add(new CourseAssignment(SubmissionSource.GoogleClassroom, "course", id, title, 100, dueAt, Now));

        public void Approve(string assignmentId, double grade, MistakeCategory[] mistakes, MistakeCategory[] strengths)
        {
            var at = Now.AddMinutes(++_approvals);
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course", assignmentId, "student", "Ana Beridze",
                $"sub-{assignmentId}", "folder", at);
            submission.RecordStatus("RETURNED", false, grade, [], at);
            var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", at, at, []);
            var grading = new AiGrading(version.Id, "model", "review", grade, 100, null, 1, [], [], "{}",
                mistakes.Select(c => new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, c, $"{c} slip", 2)), at,
                strengths.Select(c => new GradingStrength(null, null, null, c, $"{c} done well")));
            var report = new GradingReport(grading, submission, at);
            report.Approve(at);
            _submissions.Add(submission);
            _mistakes.AddRange(StudentMistake.FromApprovedReport(report, submission));
            _strengths.AddRange(StudentStrength.FromApprovedReport(report, submission));
        }

        Task<IReadOnlyList<StudentMistake>> IStudentMistakeStore.GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StudentMistake>>(_mistakes.ToList());

        Task<IReadOnlyList<StudentMistake>> IStudentMistakeStore.GetByReportAsync(Guid reportId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        void IStudentMistakeStore.AddRange(IEnumerable<StudentMistake> mistakes) => throw new NotSupportedException();

        void IStudentMistakeStore.RemoveRange(IEnumerable<StudentMistake> mistakes) => throw new NotSupportedException();

        Task<IReadOnlyList<StudentStrength>> IStudentStrengthStore.GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StudentStrength>>(_strengths.ToList());

        Task<IReadOnlyList<StudentStrength>> IStudentStrengthStore.GetByReportAsync(Guid reportId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        void IStudentStrengthStore.AddRange(IEnumerable<StudentStrength> strengths) => throw new NotSupportedException();

        void IStudentStrengthStore.RemoveRange(IEnumerable<StudentStrength> strengths) => throw new NotSupportedException();

        Task<IReadOnlyList<CourseAssignment>> ICourseAssignmentStore.GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CourseAssignment>>(_assignments);

        void ICourseAssignmentStore.Add(CourseAssignment assignment) => throw new NotSupportedException();

        Task<IReadOnlyList<Submission>> ISubmissionStore.GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Submission>>(_submissions);

        Task<IReadOnlyList<Submission>> ISubmissionStore.GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<StoredFileLocation?> ISubmissionStore.FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<Submission?> ISubmissionStore.FindByVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<string?> ISubmissionStore.FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        void ISubmissionStore.Add(Submission submission) => throw new NotSupportedException();

        Task<GradingReport?> IGradingReportStore.FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<ReportScore>> IGradingReportStore.GetScoresAsync(IReadOnlyCollection<Guid> reportIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReportScore>>([]);

        void IGradingReportStore.Add(GradingReport report) => throw new NotSupportedException();

        Task<StudentAnalysis?> IStudentAnalysisStore.GetLatestAsync(
            string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken) =>
            Task.FromResult(Analyses.LastOrDefault(a => a.Kind == kind));

        void IStudentAnalysisStore.Add(StudentAnalysis analysis) => Analyses.Add(analysis);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string ModelId => "model";

        public string GradingModelId => "model";

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
        {
            Prompt = string.Join("\n", parts.OfType<TextPart>().Select(p => p.Text));
            return Task.FromResult(new LanguageModelReply("model", Reply));
        }

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AgentReply> RunAgentAsync(
            string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
