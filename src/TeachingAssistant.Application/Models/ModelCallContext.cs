using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Models;

public class ModelCallContext
{
    public ModelCallSubject? Subject { get; private set; }

    public bool PartOfGradeAll { get; private set; }

    public bool StudentAnalysis { get; private set; }

    /// <summary>The model this scope grades with instead of the configured one; null for the configured one.</summary>
    public string? GradingModel { get; private set; }

    /// <summary>True in an evaluation run, so its calls are recorded under their own purpose.</summary>
    public bool Evaluation { get; private set; }

    /// <summary>True for "Try with model", so its calls are recorded under their own purpose.</summary>
    public bool TryOut { get; private set; }

    public void UseGradingModelForEvaluation(string model)
    {
        GradingModel = model;
        Evaluation = true;
    }

    public void UseGradingModelForTry(string model)
    {
        GradingModel = model;
        TryOut = true;
    }

    public void MarkStudentAnalysis()
    {
        StudentAnalysis = true;
    }

    public void DescribeCourse(string courseId)
    {
        Subject = new ModelCallSubject { CourseId = courseId };
    }

    public void MarkPartOfGradeAll()
    {
        PartOfGradeAll = true;
    }

    public void Describe(string courseId, string assignmentId, Guid versionId)
    {
        Subject = new ModelCallSubject { CourseId = courseId, AssignmentId = assignmentId, VersionId = versionId };
    }

    public void DescribeAssignment(string courseId, string assignmentId)
    {
        Subject = new ModelCallSubject { CourseId = courseId, AssignmentId = assignmentId };
    }
}
