using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class GradingDraftServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeGradingStore _gradings = new();

    private GradingDraftService Service() => new(_gradings, new NoReports(), new NoMistakes(), new NoStrengths(), TimeProvider.System);

    [Fact]
    public async Task Current_IsTheNewestDraft_DiscardedOnesAreSkipped()
    {
        var versionId = Guid.NewGuid();
        var first = Grade(versionId, 70, Start);
        var second = Grade(versionId, 86, Start.AddMinutes(5));
        var third = Grade(versionId, 40, Start.AddMinutes(9));

        await Service().DiscardAsync(third.Id, TestContext.Current.CancellationToken);
        var current = await Service().GetCurrentAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Equal(second.Id, current!.Id);
        Assert.Equal(86, current.Score);
        var history = await Service().GetHistoryAsync(versionId, TestContext.Current.CancellationToken);
        Assert.Equal([third.Id, second.Id, first.Id], history.Select(h => h.Id));
        Assert.Equal(GradingStatus.Discarded, history[0].Status);
    }

    [Fact]
    public async Task Current_IsNull_BeforeTheFirstGrading()
    {
        Assert.Null(await Service().GetCurrentAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Discard_UnknownGrading_ReturnsNull()
    {
        Assert.Null(await Service().DiscardAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CourseDrafts_ByDefault_AreTheCurrentDraftOfEachVersion()
    {
        var anaV1 = _gradings.Version("a1", "sub-1", "Ana K.", 1);
        var anaV2 = _gradings.Version("a1", "sub-1", "Ana K.", 2);
        var gio = _gradings.Version("a2", "sub-2", "Gio M.", 1);
        var anaOld = Grade(anaV1, 60, Start);
        var anaFirstTry = Grade(anaV2, 70, Start.AddDays(1));
        var anaRegrade = Grade(anaV2, 90, Start.AddDays(2),
            new GradingMistake(null, null, null, MistakeSeverity.Major, MistakeCategory.Correctness, "a"),
            new GradingMistake(null, null, null, MistakeSeverity.Style, MistakeCategory.Readability, "b"));
        var gioDiscarded = Grade(gio, 20, Start.AddDays(3));
        gioDiscarded.Discard(Start.AddDays(3));

        var current = await Service().GetByCourseAsync("course-1", null, null, includeAll: false, TestContext.Current.CancellationToken);
        var all = await Service().GetByCourseAsync("course-1", null, null, includeAll: true, TestContext.Current.CancellationToken);

        // v2's regrade replaced its first try; v1 keeps its own draft; Gio's only grading was discarded.
        Assert.Equal([anaRegrade.Id, anaOld.Id], current.Select(d => d.Id));
        Assert.All(current, d => Assert.True(d.Current));
        Assert.Equal((2, "a1", "Ana K.", "sub-1"), (current[0].VersionNumber, current[0].AssignmentId, current[0].StudentName, current[0].SubmissionId));
        Assert.Equal((1, 0, 1), (current[0].Major, current[0].Minor, current[0].Style));

        Assert.Equal([gioDiscarded.Id, anaRegrade.Id, anaFirstTry.Id, anaOld.Id], all.Select(d => d.Id));
        Assert.False(all.Single(d => d.Id == anaFirstTry.Id).Current);
    }

    [Fact]
    public async Task CourseDrafts_FilteredByAssignment_OnlyThatAssignment()
    {
        var ana = _gradings.Version("a1", "sub-1", "Ana K.", 1);
        var gio = _gradings.Version("a2", "sub-2", "Gio M.", 1);
        Grade(ana, 80, Start);
        var gioDraft = Grade(gio, 75, Start);

        var drafts = await Service().GetByCourseAsync("course-1", "a2", null, includeAll: false, TestContext.Current.CancellationToken);

        Assert.Equal([gioDraft.Id], drafts.Select(d => d.Id));
    }

    [Fact]
    public async Task CourseDrafts_FilteredByStudent_AreThatStudentsDraftsAcrossAssignments()
    {
        var anaA1 = _gradings.Version("a1", "sub-1", "Ana K.", 1);
        var anaA2 = _gradings.Version("a2", "sub-3", "Ana K.", 1);
        var gioA1 = _gradings.Version("a1", "sub-2", "Gio M.", 1);
        var first = Grade(anaA1, 80, Start);
        var second = Grade(anaA2, 95, Start.AddDays(1));
        Grade(gioA1, 70, Start);

        var ana = await Service().GetByCourseAsync("course-1", null, "user-Ana K.", includeAll: false, TestContext.Current.CancellationToken);
        var anaInA2 = await Service().GetByCourseAsync("course-1", "a2", "user-Ana K.", includeAll: false, TestContext.Current.CancellationToken);

        Assert.Equal([second.Id, first.Id], ana.Select(d => d.Id));
        Assert.All(ana, d => Assert.Equal("user-Ana K.", d.StudentId));
        Assert.Equal([second.Id], anaInA2.Select(d => d.Id));
    }

    private AiGrading Grade(Guid versionId, double score, DateTimeOffset at, params GradingMistake[] mistakes)
    {
        var grading = new AiGrading(versionId, "model", "review", score, 100, null, 1, [], [], "{}", mistakes, at);
        _gradings.Gradings.Add(grading);
        return grading;
    }

    private sealed class NoReports : IGradingReportStore
    {
        public Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<GradingReport?>(null);

        public Task<IReadOnlyList<ReportScore>> GetScoresAsync(IReadOnlyCollection<Guid> reportIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(GradingReport report) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoMistakes : IStudentMistakeStore
    {
        public Task<IReadOnlyList<StudentMistake>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentMistake>>([]);

        public Task<IReadOnlyList<StudentMistake>> GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentMistake>>([]);

        public void AddRange(IEnumerable<StudentMistake> mistakes) => throw new NotSupportedException();

        public void RemoveRange(IEnumerable<StudentMistake> mistakes)
        {
        }
    }

    private sealed class NoStrengths : IStudentStrengthStore
    {
        public Task<IReadOnlyList<StudentStrength>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentStrength>>([]);

        public Task<IReadOnlyList<StudentStrength>> GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentStrength>>([]);

        public void AddRange(IEnumerable<StudentStrength> strengths) => throw new NotSupportedException();

        public void RemoveRange(IEnumerable<StudentStrength> strengths)
        {
        }
    }

    // Stands in for the database: gradings plus the version → submission facts the real store joins to.
    private sealed class FakeGradingStore : IGradingStore
    {
        private readonly Dictionary<Guid, (string AssignmentId, string SubmissionId, string StudentName, int Number)> _versions = [];

        public List<AiGrading> Gradings { get; } = [];

        public Guid Version(string assignmentId, string submissionId, string studentName, int number)
        {
            var id = Guid.NewGuid();
            _versions[id] = (assignmentId, submissionId, studentName, number);
            return id;
        }

        // A version's student ID is "user-" + the student's name here.
        public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
            SubmissionSource source, string courseId, string? assignmentId, string? studentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GradingSummary>>(Gradings
                .Where(g => _versions.ContainsKey(g.VersionId))
                .Select(g => (Grading: g, Version: _versions[g.VersionId]))
                .Where(x => assignmentId is null || x.Version.AssignmentId == assignmentId)
                .Where(x => studentId is null || $"user-{x.Version.StudentName}" == studentId)
                .OrderByDescending(x => x.Grading.CreatedAt)
                .Select(x => new GradingSummary(x.Grading.Id, x.Grading.VersionId, x.Version.Number, x.Version.AssignmentId,
                    x.Version.SubmissionId, $"user-{x.Version.StudentName}", x.Version.StudentName, x.Grading.Status, x.Grading.Score, x.Grading.MaxScore,
                    x.Grading.Bonus,
                    x.Grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Major),
                    x.Grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Minor),
                    x.Grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Style),
                    x.Grading.Model, x.Grading.CreatedAt, null))
                .ToList());

        public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiGrading>>(Gradings.Where(g => versionIds.Contains(g.VersionId))
                .OrderByDescending(g => g.CreatedAt).ToList());

        public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Gradings.FirstOrDefault(g => g.Id == gradingId));

        public void Add(AiGrading grading) => Gradings.Add(grading);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
