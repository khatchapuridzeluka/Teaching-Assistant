using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class InstructionsService : IInstructionsService
{
    private const string FallbackContentType = "application/octet-stream";

    private readonly IInstructionsStore _store;
    private readonly ISnapshotStorage _storage;

    public InstructionsService(IInstructionsStore store, ISnapshotStorage storage)
    {
        _store = store;
        _storage = storage;
    }

    public async Task<StoredInstructionsResponse?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        var instructions = await _store.GetAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken);
        var latest = instructions?.Versions.MaxBy(v => v.Number);
        if (instructions is null || latest is null)
        {
            return null;
        }

        var files = latest.Contents
            .Select(c => new InstructionsFileResponse(
                c.Id, c.Name, c.Origin, c.MimeType, c.SizeBytes, c.Status, c.StoredName is not null,
                // Only links point somewhere useful; a Drive file ID is not a URL.
                c.Origin == ContentOrigin.Drive ? null : c.ExternalId))
            .ToList();

        return new StoredInstructionsResponse(latest.Id, latest.Number, instructions.Versions.Count, latest.Title,
            latest.Description, latest.MaxPoints, latest.FetchedAt, files);
    }

    public async Task<StoredFileContent?> OpenFileAsync(string assignmentId, Guid contentId, CancellationToken cancellationToken = default)
    {
        // Looked up within the assignment, so a file ID from another assignment is not found here.
        var instructions = await _store.GetAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken);
        foreach (var version in instructions?.Versions ?? [])
        {
            var content = version.Contents.FirstOrDefault(c => c.Id == contentId && c.StoredName is not null);
            if (content is null)
            {
                continue;
            }

            var stream = _storage.OpenOriginal(version.StoragePath, content.StoredName!);
            return stream is null
                ? null
                : new StoredFileContent(stream, content.MimeType ?? FallbackContentType, content.Name);
        }

        return null;
    }
}
