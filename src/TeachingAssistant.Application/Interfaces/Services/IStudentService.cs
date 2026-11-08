using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>Students and their results, read from our database only (no source calls).</summary>
public interface IStudentService
{
    Task<CourseStudentsResponse> GetCourseStudentsAsync(string courseId, CancellationToken cancellationToken = default);

    /// <summary>The stored profile photo of a student (by their Google user ID); null when there is none.</summary>
    Task<StudentPhoto?> OpenPhotoAsync(string externalUserId, CancellationToken cancellationToken = default);
}
