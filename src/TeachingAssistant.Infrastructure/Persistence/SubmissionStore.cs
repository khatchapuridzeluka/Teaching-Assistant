using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence;

/// <summary>
/// <see cref="ISubmissionStore"/> on EF Core. Submissions are loaded with their whole version history,
/// which is small (a few versions per student) and needed for change detection.
/// </summary>
public class SubmissionStore(AppDbContext db) : ISubmissionStore
{
    public async Task<IReadOnlyList<Submission>> GetByAssignmentAsync(
        SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
        await db.Submissions
            .Include(s => s.Versions)
            .ThenInclude(v => v.Contents)
            .Where(s => s.Source == source && s.AssignmentId == assignmentId)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
        db.VersionContents
            .Where(c => c.Id == contentId && c.StoredName != null)
            .Join(db.SubmissionVersions, c => c.VersionId, v => v.Id,
                (c, v) => new StoredFileLocation(v.StoragePath, c.StoredName!, c.Name, c.MimeType))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        db.SubmissionVersions
            .Where(v => v.Id == versionId)
            .Select(v => v.StoragePath)
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(Submission submission) => db.Submissions.Add(submission);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
