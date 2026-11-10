using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Models;

public class ModelCallContext
{
    public ModelCallSubject? Subject { get; private set; }

    public bool PartOfGradeAll { get; private set; }

    public bool StudentAnalysis { get; private set; }

    public void MarkStudentAnalysis()
    {
        StudentAnalysis = true;
    }

    public void DescribeCourse(string courseId)
    {
        Subject = new ModelCallSubject(courseId, null, null);
    }

    public void MarkPartOfGradeAll()
    {
        PartOfGradeAll = true;
    }

    public void Describe(string courseId, string assignmentId, Guid versionId)
    {
        Subject = new ModelCallSubject(courseId, assignmentId, versionId);
    }

    public void DescribeAssignment(string courseId, string assignmentId)
    {
        Subject = new ModelCallSubject(courseId, assignmentId, null);
    }
}
