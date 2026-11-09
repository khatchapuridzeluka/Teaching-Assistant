using System.Globalization;
using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class GradebookService : IGradebookService
{
    private readonly IStudentService _students;

    public GradebookService(IStudentService students)
    {
        _students = students;
    }

    public async Task<GradebookResponse> GetAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var course = await _students.GetCourseStudentsAsync(courseId, cancellationToken);
        var students = course.Students.Where(s => s.Active).ToList();
        var columns = course.Assignments.Select(a => Column(a, students)).ToList();
        var overall = students.Where(s => s.OverallPercent is not null).Select(s => s.OverallPercent!.Value).ToList();

        return new GradebookResponse(course.SyncedAt, columns, students, overall.Count == 0 ? null : overall.Average());
    }

    public async Task<byte[]> ExportCsvAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var gradebook = await GetAsync(courseId, cancellationToken);
        var csv = new StringBuilder();

        var header = new List<string> { "Student" };
        foreach (var assignment in gradebook.Assignments)
        {
            var max = assignment.MaxPoints is { } points ? $" (/{Number(points)})" : "";
            header.Add($"{assignment.Title}{max} initial");
            header.Add($"{assignment.Title}{max} final");
        }

        header.Add("Overall %");
        AppendRow(csv, header);

        foreach (var student in gradebook.Students)
        {
            var row = new List<string> { student.Name };
            foreach (var result in student.Results)
            {
                row.Add(result.InitialGrade is { } initial ? Number(initial) : "");
                row.Add(FinalCell(result));
            }

            row.Add(student.OverallPercent is { } percent ? Number(Math.Round(percent, 1)) : "");
            AppendRow(csv, row);
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private static GradebookColumn Column(StudentAssignmentColumn assignment, List<CourseStudentResponse> students)
    {
        var cells = students.Select(s => s.Results.Single(r => r.AssignmentId == assignment.Id)).ToList();
        var graded = cells.Where(c => c.GradeStatus == GradeStatus.Graded).ToList();
        double? average = assignment.MaxPoints is > 0 && graded.Count > 0
            ? graded.Average(c => c.FinalGrade!.Value / assignment.MaxPoints.Value * 100)
            : null;

        return new GradebookColumn(assignment.Id, assignment.Title, assignment.MaxPoints, assignment.DueAt, graded.Count,
            cells.Count(c => c.GradeStatus == GradeStatus.AwaitingGrade), cells.Count(c => c.GradeStatus == GradeStatus.Missing), average);
    }

    private static string FinalCell(StudentAssignmentResult result)
    {
        return result.GradeStatus switch
        {
            GradeStatus.Graded => Number(result.FinalGrade!.Value),
            GradeStatus.AwaitingGrade => "not graded",
            GradeStatus.Missing => "missing",
            _ => "",
        };
    }

    private static string Number(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> cells)
    {
        csv.Append(string.Join(",", cells.Select(Escape)));
        csv.Append("\r\n");
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
