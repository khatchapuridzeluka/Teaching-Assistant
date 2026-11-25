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

    private StudentAnalysisService Service() =>
        new(new StudentRecordCollector(_fakes, _fakes, _fakes, _fakes, _fakes), _fakes, _fakes, _fakes, _context, TimeProvider.System);

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
    public async Task TheAnalysis_ReadsTheCurrentDraft_ItsReportInReview_OrElseTheApprovedRecords()
    {
        _fakes.Assignment("a1", "Assignment 1", Now.AddDays(-30));
        _fakes.Assignment("a2", "Assignment 2", Now.AddDays(-20));
        _fakes.Assignment("a3", "Assignment 3", Now.AddDays(-10));
        _fakes.Approve("a1", grade: 80, mistakes: [], strengths: [MistakeCategory.Design]);
        _fakes.Resubmit("a1");
        _fakes.Grade("a2", grade: 70, mistakes: [], strengths: [MistakeCategory.Naming], report: false);
        _fakes.Grade("a3", grade: 90, mistakes: [], strengths: [MistakeCategory.Correctness], report: true);
        _fakes.Grade("a4", grade: 50, mistakes: [], strengths: [MistakeCategory.ErrorHandling], report: false, discard: true);
        _fakes.Reply = """{ "overview": "o", "groups": [] }""";

        await Service().AnalyzeAsync("course", "student", StudentAnalysisKind.Strengths, TestContext.Current.CancellationToken);

        var prompt = _fakes.Prompt!;
        Assert.Contains("[1] Assignment 1 · Design · new · Design done well", prompt);
        Assert.Contains("[2] Assignment 2 · Naming · new · Naming done well", prompt);
        Assert.Contains("[3] Assignment 3 · Correctness · new · Correctness done well", prompt);
        Assert.DoesNotContain("ErrorHandling", prompt);

        var latest = (await Service().GetLatestAsync("course", "student", StudentAnalysisKind.Strengths, TestContext.Current.CancellationToken))!;
        Assert.False(latest.Outdated);
        Assert.Equal(3, Assert.Single(latest.Groups).Count);

        var records = await new StudentRecordCollector(_fakes, _fakes, _fakes, _fakes, _fakes)
            .CollectAsync("course", "student", TestContext.Current.CancellationToken);
        Assert.Equal((StudentRecordSource.Approved, StudentRecordSource.Draft, StudentRecordSource.InReview),
            (records.SourceOf("a1"), records.SourceOf("a2"), records.SourceOf("a3")));
        Assert.Equal(((double?)70, (double?)null), (records.Assignments["a2"].Score!.AiScore, records.Assignments["a2"].Score!.Score));
    }

    [Fact]
    public async Task AReport_WinsOverANewerDraft_OfTheSameOrANewerVersion_AndWithoutReportTheNewestDraftCounts()
    {
        _fakes.Assignment("a1", "Assignment 1", Now.AddDays(-30));
        _fakes.Assignment("a2", "Assignment 2", Now.AddDays(-20));
        _fakes.Assignment("a3", "Assignment 3", Now.AddDays(-10));
        _fakes.Grade("a1", grade: 60, mistakes: [], strengths: [MistakeCategory.Design], report: true);
        _fakes.GradeAgain("a1", grade: 99, mistakes: [], strengths: [MistakeCategory.Naming]);
        _fakes.Approve("a2", grade: 80, mistakes: [], strengths: [MistakeCategory.Correctness]);
        _fakes.Resubmit("a2");
        _fakes.GradeAgain("a2", grade: 95, mistakes: [], strengths: [MistakeCategory.Readability]);
        _fakes.Grade("a3", grade: 50, mistakes: [], strengths: [MistakeCategory.Encapsulation], report: false);
        _fakes.GradeAgain("a3", grade: 70, mistakes: [], strengths: [MistakeCategory.ErrorHandling]);

        var records = await new StudentRecordCollector(_fakes, _fakes, _fakes, _fakes, _fakes)
            .CollectAsync("course", "student", TestContext.Current.CancellationToken);

        Assert.Equal(
            [MistakeCategory.Design, MistakeCategory.Correctness, MistakeCategory.ErrorHandling],
            records.Strengths.OrderBy(s => s.AssignmentId).Select(s => s.Category));
        Assert.Equal((StudentRecordSource.InReview, StudentRecordSource.Approved, StudentRecordSource.Draft),
            (records.SourceOf("a1"), records.SourceOf("a2"), records.SourceOf("a3")));
        Assert.Equal((double?)70, records.Assignments["a3"].Score!.AiScore);
    }

    [Fact]
    public async Task OverviewAnalysis_SendsScoresMistakesAndStrengths_WithExactNumbers_NeverTheName()
    {
        _fakes.Assignment("a1", "Assignment 1", Now.AddDays(-20));
        _fakes.Assignment("a2", "Assignment 2", Now.AddDays(-10));
        _fakes.Approve("a1", grade: 70, mistakes: [MistakeCategory.ErrorHandling, MistakeCategory.Naming], strengths: [MistakeCategory.Design]);
        _fakes.Grade("a2", grade: 90, mistakes: [MistakeCategory.Naming], strengths: [MistakeCategory.ErrorHandling], report: false);
        _fakes.Reply = """{ "summary": " Getting better. ", "nextSteps": "Keep naming in check." }""";

        await Service().AnalyzeAsync("course", "student", StudentAnalysisKind.Overview, TestContext.Current.CancellationToken);

        var prompt = _fakes.Prompt!;
        Assert.DoesNotContain("Ana Beridze", prompt);
        Assert.Contains("score=\"70 / 100\" checked=\"teacher's report\"", prompt);
        Assert.Contains("score=\"90 / 100\" checked=\"AI draft, not reviewed\"", prompt);
        Assert.Contains("- mistake · Minor · Naming · -2 pts · repeated (first in Assignment 1)", prompt);
        Assert.Contains("- strength · ErrorHandling · improved (was a mistake in Assignment 1)", prompt);
        Assert.Contains("2 graded assignments, average score 80%, first 70%, latest 90%", prompt);
        Assert.Contains("3 mistakes (1 repeated), 6 points lost", prompt);
        Assert.Contains("ErrorHandling: 1 / 1 / 2", prompt);

        var latest = (await Service().GetLatestAsync("course", "student", StudentAnalysisKind.Overview, TestContext.Current.CancellationToken))!;
        Assert.Equal((StudentAnalysisKind.Overview, "Getting better.", "Keep naming in check.", 2, 5, false),
            (latest.Kind, latest.Summary, latest.NextSteps, latest.Assignments, latest.Records, latest.Outdated));
    }

    [Fact]
    public async Task Overview_CountsScoresFromTheReportOrElseTheDraft_PerAssignmentInDueOrder()
    {
        _fakes.Assignment("a1", "Assignment 1", Now.AddDays(-20));
        _fakes.Assignment("a2", "Assignment 2", Now.AddDays(-10));
        _fakes.Grade("a2", grade: 90, mistakes: [MistakeCategory.Naming], strengths: [], report: false);
        _fakes.Approve("a1", grade: 60, mistakes: [MistakeCategory.Naming, MistakeCategory.Design], strengths: [MistakeCategory.Readability]);

        var overview = await new StudentOverviewService(new StudentRecordCollector(_fakes, _fakes, _fakes, _fakes, _fakes), _fakes)
            .GetAsync("course", "student", TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "a2"], overview.Assignments.Select(a => a.AssignmentId));
        Assert.Equal(((double?)60, StudentRecordSource.Approved, 2), (overview.Assignments[0].Percent, overview.Assignments[0].Source, overview.Assignments[0].Mistakes));
        Assert.Equal(((double?)90, StudentRecordSource.Draft, 1), (overview.Assignments[1].Percent, overview.Assignments[1].Source, overview.Assignments[1].Repeated));
        Assert.Equal(((double?)75, (double?)60, (double?)90), (overview.AveragePercent, overview.FirstPercent, overview.LatestPercent));
        Assert.Equal((3, 1, 6.0, 1), (overview.Mistakes, overview.RepeatedMistakes, overview.PointsLost, overview.Strengths));
        Assert.Equal(MistakeCategory.Naming, overview.TopMistakes[0].Category);
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
        IGradingStore, IStudentAnalysisStore, ILanguageModel
    {
        public Task<IReadOnlyList<TeachingAssistant.Application.Models.DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TeachingAssistant.Application.Models.ApprovedReport>> GetApprovedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private readonly List<AiGrading> _gradings = [];
        private readonly List<GradingReport> _reports = [];

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
            var (submission, grading) = Graded(assignmentId, grade, mistakes, strengths);
            var report = new GradingReport(grading, submission, grading.CreatedAt);
            report.Approve(grading.CreatedAt);
            _reports.Add(report);
            _mistakes.AddRange(StudentMistake.FromApprovedReport(report, submission));
            _strengths.AddRange(StudentStrength.FromApprovedReport(report, submission));
        }

        public void Grade(
            string assignmentId, double grade, MistakeCategory[] mistakes, MistakeCategory[] strengths, bool report, bool discard = false)
        {
            var (submission, grading) = Graded(assignmentId, grade, mistakes, strengths);
            if (report)
            {
                _reports.Add(new GradingReport(grading, submission, grading.CreatedAt));
            }

            if (discard)
            {
                grading.Discard(grading.CreatedAt);
            }
        }

        public void GradeAgain(string assignmentId, double grade, MistakeCategory[] mistakes, MistakeCategory[] strengths)
        {
            var at = Now.AddMinutes(++_approvals);
            var submission = _submissions.Single(s => s.AssignmentId == assignmentId);
            var grading = new AiGrading(submission.LatestVersion!.Id, "model", "review", grade, 100, null, 1, [], [], "{}",
                mistakes.Select(c => new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, c, $"{c} slip", 2)), at,
                strengths.Select(c => new GradingStrength(null, null, null, c, $"{c} done well")));
            _gradings.Add(grading);
        }

        public void Resubmit(string assignmentId)
        {
            var at = Now.AddMinutes(++_approvals);
            _submissions.Single(s => s.AssignmentId == assignmentId).AddVersion(VersionStatus.Ready, null, "hash2", "path2", at, at, []);
        }

        private (Submission Submission, AiGrading Grading) Graded(
            string assignmentId, double grade, MistakeCategory[] mistakes, MistakeCategory[] strengths)
        {
            var at = Now.AddMinutes(++_approvals);
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course", assignmentId, "student", "Ana Beridze",
                $"sub-{assignmentId}", "folder", at);
            submission.RecordStatus("RETURNED", false, grade, [], at);
            var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", at, at, []);
            var grading = new AiGrading(version.Id, "model", "review", grade, 100, null, 1, [], [], "{}",
                mistakes.Select(c => new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, c, $"{c} slip", 2)), at,
                strengths.Select(c => new GradingStrength(null, null, null, c, $"{c} done well")));
            _submissions.Add(submission);
            _gradings.Add(grading);
            return (submission, grading);
        }

        Task<IReadOnlyList<GradingSummary>> IGradingStore.GetSummariesAsync(
            SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GradingSummary>>(_gradings
                .Select(g =>
                {
                    var submission = _submissions.Single(s => s.Versions.Any(v => v.Id == g.VersionId));
                    var number = submission.Versions.Single(v => v.Id == g.VersionId).Number;
                    return new GradingSummary
                    {
                        Id = g.Id,
                        VersionId = g.VersionId,
                        VersionNumber = number,
                        AssignmentId = submission.AssignmentId,
                        SubmissionId = submission.ExternalSubmissionId,
                        StudentId = submission.StudentId,
                        StudentName = submission.StudentName,
                        Status = g.Status,
                        Score = g.Score,
                        MaxScore = g.MaxScore,
                        Bonus = g.Bonus,
                        Major = 0,
                        Minor = 0,
                        Style = 0,
                        Model = g.Model,
                        CreatedAt = g.CreatedAt,
                        ReportStatus = _reports.FirstOrDefault(r => r.GradingId == g.Id)?.Status,
                    };
                })
                .ToList());

        Task<IReadOnlyList<AiGrading>> IGradingStore.GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<AiGrading?> IGradingStore.FindAsync(Guid gradingId, CancellationToken cancellationToken) =>
            Task.FromResult(_gradings.FirstOrDefault(g => g.Id == gradingId));

        void IGradingStore.Add(AiGrading grading) => throw new NotSupportedException();

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
            Task.FromResult(_submissions.FirstOrDefault(s => s.Versions.Any(v => v.Id == versionId)));

        Task<string?> ISubmissionStore.FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        void ISubmissionStore.Add(Submission submission) => throw new NotSupportedException();

        Task<GradingReport?> IGradingReportStore.FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken) =>
            Task.FromResult(_reports.FirstOrDefault(r => r.GradingId == gradingId));

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

        public string AdapterModelId => "adapter-model";

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
        {
            Prompt = string.Join("\n", parts.OfType<TextPart>().Select(p => p.Text));
            return Task.FromResult(new LanguageModelReply { Model = "model", Text = Reply });
        }

        public Task<LanguageModelReply> WriteAdapterAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> DesignTestsAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AgentReply> RunAgentAsync(
            string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools, AgentRunHooks? hooks = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, string? model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
