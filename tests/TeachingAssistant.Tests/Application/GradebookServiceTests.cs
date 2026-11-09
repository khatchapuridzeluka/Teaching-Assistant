using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class GradebookServiceTests
{
    private readonly FakeStudents _students = new();

    [Fact]
    public async Task Columns_CountStates_AndAverageOnlyGradedWork_ForEnrolledStudents()
    {
        _students.Add("Ana", active: true, Result(GradeStatus.Graded, 80), overall: 80);
        _students.Add("Beka", active: true, Result(GradeStatus.Graded, 100), overall: 100);
        _students.Add("Dato", active: true, Result(GradeStatus.Missing, null), overall: 0);
        _students.Add("Left", active: false, Result(GradeStatus.Graded, 10), overall: 10);

        var gradebook = await new GradebookService(_students).GetAsync("course", TestContext.Current.CancellationToken);

        var column = Assert.Single(gradebook.Assignments);
        Assert.Equal((2, 0, 1, 90.0), (column.Graded, column.AwaitingGrade, column.Missing, column.AveragePercent));
        Assert.Equal(3, gradebook.Students.Count);
        Assert.Equal(60, gradebook.ClassAveragePercent);
    }

    [Fact]
    public async Task Csv_HasInitialAndFinalColumns_AndQuotesNamesWithCommas()
    {
        _students.Add("Khachapuridze, Luka", active: true, Result(GradeStatus.Graded, 95, initial: 80), overall: 95);
        _students.Add("Nini", active: true, Result(GradeStatus.AwaitingGrade, null), overall: null);

        var bytes = await new GradebookService(_students).ExportCsvAsync("course", TestContext.Current.CancellationToken);
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Student,Task 1 (/100) initial,Task 1 (/100) final,Overall %", lines[0]);
        Assert.Equal("\"Khachapuridze, Luka\",80,95,95", lines[1]);
        Assert.Equal("Nini,,not graded,", lines[2]);
    }

    private static StudentAssignmentResult Result(GradeStatus state, double? final, double? initial = null) =>
        new("a1", "sub", null, state is GradeStatus.Graded or GradeStatus.AwaitingGrade, false,
            state == GradeStatus.Missing, state, initial ?? final, final, []);

    private sealed class FakeStudents : IStudentService
    {
        private readonly List<CourseStudentResponse> _rows = [];

        public void Add(string name, bool active, StudentAssignmentResult result, double? overall) =>
            _rows.Add(new CourseStudentResponse(Guid.NewGuid(), name, name, active, 1, 0, 0, 0, overall, 0, 0, [result]));

        public Task<CourseStudentsResponse> GetCourseStudentsAsync(string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CourseStudentsResponse(null, [new StudentAssignmentColumn("a1", "Task 1", 100, null)], _rows));

        public Task<StudentPhoto?> OpenPhotoAsync(string externalUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<StudentPhoto?>(null);
    }
}
