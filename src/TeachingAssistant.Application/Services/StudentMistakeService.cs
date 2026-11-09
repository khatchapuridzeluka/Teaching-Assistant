using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

public class StudentMistakeService : IStudentMistakeService
{
    private readonly IStudentMistakeStore _mistakes;

    public StudentMistakeService(IStudentMistakeStore mistakes)
    {
        _mistakes = mistakes;
    }

    public async Task<IReadOnlyList<StudentMistakeResponse>> GetAsync(
        string courseId, string studentId, CancellationToken cancellationToken = default)
    {
        var mistakes = await _mistakes.GetByStudentAsync(courseId, studentId, cancellationToken);
        return mistakes
            .Select(m => new StudentMistakeResponse(m.Id, m.AssignmentId, m.VersionId, m.ReportId, m.File, m.StartLine, m.EndLine,
                m.Severity, m.Category, m.Message, m.Points, m.Decision, m.ApprovedAt))
            .ToList();
    }
}
