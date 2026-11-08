using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Domain;

public class StudentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EnrollIn_TwiceForTheSameCourse_KeepsOneEnrollment()
    {
        var student = NewStudent();

        student.EnrollIn("course-1", Now);
        student.EnrollIn("course-1", Now.AddDays(1));

        var enrollment = Assert.Single(student.Enrollments);
        Assert.Equal(Now, enrollment.EnrolledAt);
        Assert.Equal(Now.AddDays(1), enrollment.LastSeenAt);
        Assert.Equal(student.Id, enrollment.StudentId);
    }

    [Fact]
    public void LeaveCourse_KeepsTheEnrollmentInactive_AndRejoiningActivatesIt()
    {
        var student = NewStudent();
        student.EnrollIn("course-1", Now);
        student.EnrollIn("course-2", Now);

        student.LeaveCourse("course-1");

        Assert.False(student.IsEnrolledIn("course-1"));
        Assert.True(student.IsEnrolledIn("course-2"));
        Assert.Equal(2, student.Enrollments.Count);

        student.EnrollIn("course-1", Now.AddDays(2));
        Assert.True(student.IsEnrolledIn("course-1"));
    }

    [Fact]
    public void NeedsPhoto_WhenThereIsOneAndNoCopy_OrTheUrlChanged_OrTheCopyIsOld()
    {
        var student = NewStudent();

        Assert.False(student.NeedsPhoto(null, Now)); // no photo in the source
        Assert.True(student.NeedsPhoto("https://photo/a", Now)); // no copy yet

        student.RecordPhoto("https://photo/a", "hash-a", "u1.jpg", "image/jpeg", Now);
        Assert.False(student.NeedsPhoto("https://photo/a", Now.AddDays(1)));
        Assert.True(student.NeedsPhoto("https://photo/b", Now.AddDays(1)));
        Assert.True(student.NeedsPhoto("https://photo/a", Now + Student.PhotoRecheckAfter + TimeSpan.FromHours(1)));
    }

    [Fact]
    public void RecordPhoto_SameBytesBehindANewUrl_IsNotAChange()
    {
        var student = NewStudent();
        Assert.True(student.RecordPhoto("https://photo/a", "hash-a", "u1.jpg", "image/jpeg", Now));

        var changed = student.RecordPhoto("https://photo/a2", "hash-a", "u1.jpg", "image/jpeg", Now.AddDays(2));

        Assert.False(changed);
        Assert.Equal("https://photo/a2", student.PhotoUrl);
        Assert.Equal(Now.AddDays(2), student.PhotoFetchedAt);
        Assert.True(student.RecordPhoto("https://photo/b", "hash-b", "u1.jpg", "image/jpeg", Now.AddDays(3)));
    }

    [Fact]
    public void RemovePhoto_ForgetsEverything()
    {
        var student = NewStudent();
        student.RecordPhoto("https://photo/a", "hash-a", "u1.jpg", "image/jpeg", Now);

        student.RemovePhoto();

        Assert.Equal((null, null, null), (student.PhotoUrl, student.PhotoHash, student.PhotoStoredName));
    }

    private static Student NewStudent() => new(SubmissionSource.GoogleClassroom, "user-1", "Nino B.", Now);
}
