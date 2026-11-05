namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// Where a submission is listed (not where its files come from; see <see cref="ContentOrigin"/>).
/// </summary>
public enum SubmissionSource
{
    GoogleClassroom,
    Upload,
}
