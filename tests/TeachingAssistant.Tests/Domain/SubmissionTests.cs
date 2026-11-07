using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Domain;

public class SubmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HasChanged_NoVersionYet_IsTrue()
    {
        var submission = NewSubmission();

        Assert.True(submission.HasChanged([Drive("file-1", "md5-a")]));
    }

    [Fact]
    public void HasChanged_SameContentInAnyOrder_IsFalse()
    {
        var submission = NewSubmission();
        AddVersion(submission, Content("file-1", "md5-a"), Content("file-2", "md5-b"));

        Assert.False(submission.HasChanged([Drive("file-2", "md5-b"), Drive("file-1", "md5-a")]));
    }

    [Fact]
    public void HasChanged_ReplacedFile_IsTrue()
    {
        var submission = NewSubmission();
        AddVersion(submission, Content("file-1", "md5-a"));

        Assert.True(submission.HasChanged([Drive("file-1", "md5-changed")]));
    }

    [Fact]
    public void HasChanged_AddedOrRemovedFile_IsTrue()
    {
        var submission = NewSubmission();
        AddVersion(submission, Content("file-1", "md5-a"));

        Assert.True(submission.HasChanged([Drive("file-1", "md5-a"), Drive("file-2", "md5-b")]));
        Assert.True(submission.HasChanged([]));
    }

    [Fact]
    public void AddVersion_NumbersVersionsInOrder()
    {
        var submission = NewSubmission();

        var first = AddVersion(submission, Content("file-1", "md5-a"));
        var second = AddVersion(submission, Content("file-1", "md5-b"));

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
        Assert.Same(second, submission.LatestVersion);
        Assert.Equal(3, submission.NextVersionNumber);
        Assert.Equal(submission.Id, second.SubmissionId);
    }

    [Fact]
    public void AddVersion_StoresTimesInUtc()
    {
        var submission = NewSubmission();
        var tbilisi = new DateTimeOffset(2026, 11, 5, 16, 0, 0, TimeSpan.FromHours(4));

        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "submissions/x/v1", tbilisi, tbilisi, []);

        Assert.Equal(TimeSpan.Zero, version.FetchedAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), version.FetchedAt);
    }

    [Fact]
    public void RecordStatus_CopiesGradesAndHistory_InitialAndFinalFollowTheHistory()
    {
        var submission = NewSubmission();

        submission.RecordStatus("RETURNED", late: true, assignedGrade: 95,
            [Assigned(95, Now.AddDays(5)), Assigned(70, Now)], Now.AddDays(6));

        Assert.Equal(("RETURNED", true, 95.0), (submission.State, submission.Late, submission.AssignedGrade));
        Assert.Equal([70.0, 95.0], submission.GradeEvents.Select(e => e.PointsEarned!.Value));
        Assert.All(submission.GradeEvents, e => Assert.Equal(submission.Id, e.SubmissionId));
        Assert.Equal(70, submission.InitialGrade);
        Assert.Equal(95, submission.FinalGrade);
    }

    [Fact]
    public void RecordStatus_WithTheSameHistory_KeepsTheExistingEvents()
    {
        var submission = NewSubmission();
        submission.RecordStatus("RETURNED", false, 70, [Assigned(70, Now)], Now);
        var first = submission.GradeEvents.Single();

        submission.RecordStatus("RETURNED", false, 70, [Assigned(70, Now)], Now.AddHours(1));

        Assert.Same(first, submission.GradeEvents.Single());
        Assert.Equal(Now.AddHours(1), submission.StatusSyncedAt);
    }

    [Fact]
    public void RecordStatus_WithANewGrade_ReplacesTheHistory()
    {
        var submission = NewSubmission();
        submission.RecordStatus("RETURNED", false, 70, [Assigned(70, Now)], Now);

        submission.RecordStatus("RETURNED", false, 90, [Assigned(70, Now), Assigned(90, Now.AddDays(2))], Now.AddDays(2));

        Assert.Equal(2, submission.GradeEvents.Count);
        Assert.Equal(90, submission.FinalGrade);
    }

    private static GradeEvent Assigned(double points, DateTimeOffset at) => new(GradeEventKind.AssignedGrade, points, 100, at);

    private static Submission NewSubmission() =>
        new(SubmissionSource.GoogleClassroom, "course-1", "assignment-1", "student-1", "Nino B.", "submission-1",
            "submissions/Java II/Assignment 4/Nino B. (1)", Now);

    private static SubmissionVersion AddVersion(Submission submission, params VersionContent[] contents) =>
        submission.AddVersion(VersionStatus.Ready, null, "hash", $"submissions/{submission.Id}/v{submission.NextVersionNumber}",
            Now, Now, contents);

    private static VersionContent Content(string fileId, string md5) =>
        new(ContentOrigin.Drive, ContentKind.CodeArchive, fileId, "homework.zip", "application/zip", md5,
            null, 1024, "homework.zip", null, ContentStatus.Extracted);

    private static ContentFingerprint Drive(string fileId, string md5) => new(ContentOrigin.Drive, fileId, md5);
}
