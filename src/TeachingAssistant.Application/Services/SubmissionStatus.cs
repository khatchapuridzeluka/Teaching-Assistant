using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

internal static class SubmissionStatus
{
    public static void Record(Submission submission, SubmissionResponse item, DateTimeOffset syncedAt)
    {
        submission.UpdateStudentName(item.StudentName);
        submission.RecordStatus(item.State, item.Late, item.AssignedGrade,
            item.GradeHistory.Select(g => new GradeEvent(g.Kind, g.PointsEarned, g.MaxPoints, g.At)), syncedAt);
    }
}
