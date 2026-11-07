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

    private static Student NewStudent() => new(SubmissionSource.GoogleClassroom, "user-1", "Nino B.", Now);
}
