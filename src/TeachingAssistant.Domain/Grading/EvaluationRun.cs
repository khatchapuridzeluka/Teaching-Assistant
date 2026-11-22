namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// One evaluation run: the approved reports of one course (or all) graded again on the side by one or more models, to compare
/// the models on the same homework against the teacher's final word. Nothing it produces is a draft: it never replaces,
/// discards or adds to a student's gradings.
/// </summary>
public class EvaluationRun
{
    private EvaluationRun()
    {
    }

    public EvaluationRun(string? courseId, IEnumerable<string> models, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CourseId = courseId;
        Models = models.ToArray();
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    /// <summary>The course whose approved reports were graded again; null for all courses.</summary>
    public string? CourseId { get; private set; }

    public string[] Models { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }
}
