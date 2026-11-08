using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IStudentService
{
    Task<CourseStudentsResponse> GetCourseStudentsAsync(string courseId, CancellationToken cancellationToken = default);

    Task<StudentPhoto?> OpenPhotoAsync(string externalUserId, CancellationToken cancellationToken = default);
}
