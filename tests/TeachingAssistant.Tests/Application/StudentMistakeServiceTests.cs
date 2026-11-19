using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class StudentMistakeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeMistakes _mistakes = new();
    private readonly FakeAssignments _assignments = new();
    private readonly FakeSubmissions _submissions = new();

    public StudentMistakeServiceTests()
    {
        _assignments.Add("a1", "Assignment 1", Now.AddDays(-20));
        _assignments.Add("a2", "Assignment 2", Now.AddDays(-10));
        _mistakes.Approve("a1", (MistakeSeverity.Major, MistakeCategory.Correctness, 5), (MistakeSeverity.Style, MistakeCategory.Readability, 0));
        _mistakes.Approve("a2", (MistakeSeverity.Major, MistakeCategory.Correctness, 5), (MistakeSeverity.Minor, MistakeCategory.Naming, 3));
    }

    private StudentMistakeService Service() =>
        new(new StudentRecordCollector(new NoDrafts(), _mistakes, _submissions, _mistakes, new NoDrafts()), _assignments, _submissions);

    [Fact]
    public async Task Breakdowns_GiveCountsAndPercents_PerCategorySeverityLabelAndAssignment()
    {
        var result = await Service().GetAsync("course", "student", null, TestContext.Current.CancellationToken);

        Assert.Equal((4, 4, 13.0), (result.Total, result.Recorded, result.PointsLost));
        Assert.Equal((MistakeCategory.Correctness, 2, 50.0), (result.Categories[0].Category, result.Categories[0].Count, result.Categories[0].Percent));
        Assert.Equal([(MistakeSeverity.Major, 2, 50.0), (MistakeSeverity.Minor, 1, 25.0), (MistakeSeverity.Style, 1, 25.0)],
            result.Severities.Select(s => (s.Key, s.Count, s.Percent)));
        Assert.Equal((1, 25.0), (result.Labels.Single(l => l.Key == MistakeLabel.Repeated).Count, result.Labels.Single(l => l.Key == MistakeLabel.Repeated).Percent));
        Assert.Equal(["Assignment 1", "Assignment 2"], result.Assignments.Select(a => a.Title));
        Assert.Equal((2, 1, 0, 1, 1, 8.0), (result.Assignments[1].Total, result.Assignments[1].Major, result.Assignments[1].Style,
            result.Assignments[1].Minor, result.Assignments[1].Repeated, result.Assignments[1].PointsLost));
        Assert.Equal("Assignment 2", result.Mistakes[0].AssignmentTitle);
        Assert.All(result.Mistakes, m => Assert.Equal(StudentRecordSource.Approved, m.Source));
    }

    [Fact]
    public async Task Scores_ShowTheRealClassroomGradeNextToYourReportAndTheAi_PointsLostComeFromClassroom()
    {
        _submissions.Return("a2", 90);

        var result = await Service().GetAsync("course", "student", null, TestContext.Current.CancellationToken);

        var a2 = result.Assignments.Single(a => a.AssignmentId == "a2");
        Assert.Equal(((double?)100, (double?)90, (double?)88, (double?)82), (a2.MaxPoints, a2.ClassroomGrade, a2.ReportScore, a2.AiScore));
        var a1 = result.Assignments.Single(a => a.AssignmentId == "a1");
        Assert.Null(a1.ClassroomGrade);
        Assert.Equal(((double?)10, 1), (result.ClassroomPointsLost, result.ClassroomGraded));
        Assert.Equal(13.0, result.PointsLost);
    }

    [Fact]
    public async Task Filtering_NarrowsTheList_ButNeverChangesALabel()
    {
        var onlyA2 = await Service().GetAsync("course", "student", new StudentMistakeFilter(AssignmentId: "a2"), TestContext.Current.CancellationToken);
        var repeated = await Service().GetAsync("course", "student", new StudentMistakeFilter(Label: MistakeLabel.Repeated), TestContext.Current.CancellationToken);
        var major = await Service().GetAsync("course", "student", new StudentMistakeFilter(Severity: MistakeSeverity.Major), TestContext.Current.CancellationToken);

        Assert.Equal((2, 4), (onlyA2.Total, onlyA2.Recorded));
        Assert.Equal(MistakeLabel.Repeated, onlyA2.Mistakes.Single(m => m.Category == MistakeCategory.Correctness).Label);
        var mistake = Assert.Single(repeated.Mistakes);
        Assert.Equal(("a2", "Assignment 1"), (mistake.AssignmentId, mistake.RepeatedFromAssignmentTitle));
        Assert.Equal(100.0, major.Severities.Single(s => s.Key == MistakeSeverity.Major).Percent);
        Assert.Equal(2, onlyA2.AssignmentOptions.Count);
    }

    private sealed class FakeMistakes : IStudentMistakeStore, IGradingReportStore
    {
        public Task<IReadOnlyList<TeachingAssistant.Application.Models.ApprovedReport>> GetApprovedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private readonly List<StudentMistake> _mistakes = [];
        private readonly List<ReportScore> _scores = [];

        public void Approve(string assignmentId, params (MistakeSeverity Severity, MistakeCategory Category, double Points)[] mistakes)
        {
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course", assignmentId, "student", "Ana", $"sub-{assignmentId}", "folder", Now);
            var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []);
            var grading = new AiGrading(version.Id, "model", "review", 80, 100, null, 1, [], [], "{}",
                mistakes.Select(m => new GradingMistake("A.java", 1, 1, m.Severity, m.Category, $"{m.Category} problem", m.Points)), Now);
            var report = new GradingReport(grading, submission, Now);
            report.Save(grading, 88, null, "review", [], Now);
            report.Approve(Now);
            _mistakes.AddRange(StudentMistake.FromApprovedReport(report, submission));
            _scores.Add(new ReportScore(report.Id, 88, null, 82, null, 100));
        }

        public Task<IReadOnlyList<ReportScore>> GetScoresAsync(IReadOnlyCollection<Guid> reportIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReportScore>>(_scores.Where(s => reportIds.Contains(s.ReportId)).ToList());

        public Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(GradingReport report) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<StudentMistake>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentMistake>> GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentMistake>>(_mistakes);

        public void AddRange(IEnumerable<StudentMistake> mistakes) => throw new NotSupportedException();

        public void RemoveRange(IEnumerable<StudentMistake> mistakes) => throw new NotSupportedException();
    }

    private sealed class NoDrafts : IGradingStore, IStudentStrengthStore
    {
        public Task<IReadOnlyList<TeachingAssistant.Application.Models.DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
            SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GradingSummary>>([]);

        public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(AiGrading grading) => throw new NotSupportedException();

        public Task<IReadOnlyList<StudentStrength>> GetByStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentStrength>>([]);

        public Task<IReadOnlyList<StudentStrength>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void AddRange(IEnumerable<StudentStrength> strengths) => throw new NotSupportedException();

        public void RemoveRange(IEnumerable<StudentStrength> strengths) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSubmissions : ISubmissionStore
    {
        private readonly List<Submission> _submissions = [];

        public void Return(string assignmentId, double grade)
        {
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course", assignmentId, "student", "Ana", $"sub-{assignmentId}", "folder", Now);
            submission.RecordStatus("RETURNED", false, grade, [], Now);
            _submissions.Add(submission);
        }

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Submission>>(_submissions);

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => _submissions.Add(submission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAssignments : ICourseAssignmentStore
    {
        private readonly List<CourseAssignment> _assignments = [];

        public void Add(string id, string title, DateTimeOffset dueAt) =>
            _assignments.Add(new CourseAssignment(SubmissionSource.GoogleClassroom, "course", id, title, 100, dueAt, Now));

        public Task<IReadOnlyList<CourseAssignment>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CourseAssignment>>(_assignments);

        public void Add(CourseAssignment assignment) => _assignments.Add(assignment);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
