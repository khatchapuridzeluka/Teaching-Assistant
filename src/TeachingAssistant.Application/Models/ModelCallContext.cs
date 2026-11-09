using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Models;

public class ModelCallContext
{
    public ModelCallSubject? Subject { get; private set; }

    public void Describe(string courseId, string assignmentId, Guid versionId)
    {
        Subject = new ModelCallSubject(courseId, assignmentId, versionId);
    }
}
