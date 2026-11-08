using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class InstructionsFetchService : IInstructionsFetchService
{
    private const long MaxFileBytes = 20L * 1024 * 1024;

    private readonly IInstructionsStore _store;
    private readonly IRemoteFileSource _fileSource;
    private readonly ISnapshotStorage _storage;
    private readonly IDocumentReader _documentReader;
    private readonly TimeProvider _timeProvider;

    public InstructionsFetchService(
        IInstructionsStore store,
        IRemoteFileSource fileSource,
        ISnapshotStorage storage,
        IDocumentReader documentReader,
        TimeProvider timeProvider)
    {
        _store = store;
        _fileSource = fileSource;
        _storage = storage;
        _documentReader = documentReader;
        _timeProvider = timeProvider;
    }

    public async Task<InstructionsFetchResult> FetchAsync(
        string courseId, string courseName, AssignmentResponse assignment, CancellationToken cancellationToken = default)
    {
        var existing = await _store.GetAsync(SubmissionSource.GoogleClassroom, assignment.Id, cancellationToken);
        var current = await DescribeAsync(assignment.Materials, cancellationToken);

        if (existing is { LatestVersion: { } latest }
            && !existing.HasChanged(assignment.Title, assignment.Description, assignment.MaxPoints, current.Select(c => c.Fingerprint)))
        {
            return new InstructionsFetchResult(latest.Number, IsNew: false, await RestoreMissingAsync(latest, current, cancellationToken));
        }

        var instructions = existing ?? new AssignmentInstructions(SubmissionSource.GoogleClassroom, courseId, assignment.Id,
            _storage.GetInstructionsFolder(courseName, assignment.Title, assignment.Id), _timeProvider.GetUtcNow());

        var problems = new List<string>();
        var contents = new List<InstructionsContent>();

        await using (var writer = _storage.BeginVersion(instructions.StorageFolder, instructions.NextVersionNumber))
        {
            foreach (var material in current)
            {
                contents.Add(await StoreAsync(writer, material, problems, cancellationToken));
            }

            await writer.CommitAsync(cancellationToken);

            var version = instructions.AddVersion(assignment.Title, assignment.Description, assignment.MaxPoints,
                writer.RelativePath, _timeProvider.GetUtcNow(), contents);
            if (existing is null)
            {
                _store.Add(instructions);
            }

            await _store.SaveChangesAsync(cancellationToken);
            return new InstructionsFetchResult(version.Number, IsNew: true, problems);
        }
    }

    private async Task<List<string>> RestoreMissingAsync(
        InstructionsVersion version, List<CurrentMaterial> current, CancellationToken cancellationToken)
    {
        var restored = new List<string>();
        foreach (var content in version.Contents)
        {
            if (content.StoredName is not { } storedName || _storage.OriginalExists(version.StoragePath, storedName))
            {
                continue;
            }

            var file = current.FirstOrDefault(c => c.Fingerprint.ExternalId == content.ExternalId)?.File;
            if (file is null)
            {
                continue;
            }

            await _storage.RestoreOriginalAsync(version.StoragePath, storedName,
                stream => _fileSource.DownloadAsync(file, stream, cancellationToken), cancellationToken);
            restored.Add($"'{content.Name}' was missing from storage and was downloaded again (v{version.Number}).");
        }

        return restored;
    }

    private async Task<List<CurrentMaterial>> DescribeAsync(
        IReadOnlyList<SubmissionAttachment> materials, CancellationToken cancellationToken)
    {
        var current = new List<CurrentMaterial>();
        foreach (var material in materials)
        {
            if (material is { Kind: AttachmentKind.DriveFile, FileId: { } fileId })
            {
                var file = await _fileSource.GetFileAsync(fileId, cancellationToken);
                current.Add(new CurrentMaterial(new ContentFingerprint(ContentOrigin.Drive, file.Id, file.Revision), material.Title, file));
            }
            else
            {
                var id = material.Url ?? material.Title;
                current.Add(new CurrentMaterial(new ContentFingerprint(ContentOrigin.Link, id, id), material.Title, null));
            }
        }

        return current;
    }

    private async Task<InstructionsContent> StoreAsync(
        ISnapshotWriter writer, CurrentMaterial material, List<string> problems, CancellationToken cancellationToken)
    {
        if (material.File is not { } file)
        {
            return new InstructionsContent(ContentOrigin.Link, material.Fingerprint.ExternalId, material.Title, null,
                material.Fingerprint.Fingerprint, null, null, ContentStatus.Unsupported);
        }

        var usable = file.IsDownloadable
                     && (FilePart.IsSupported(file.DownloadMimeType) || _documentReader.CanRead(file.DownloadName, file.DownloadMimeType));
        string? storedName = null;
        ContentStatus status;

        if (!usable)
        {
            status = ContentStatus.Unsupported;
        }
        else if (file.SizeBytes > MaxFileBytes)
        {
            status = ContentStatus.TooLarge;
            problems.Add($"'{file.Name}' is larger than {MaxFileBytes / 1024 / 1024} MB and was not stored.");
        }
        else
        {
            storedName = await writer.AddOriginalAsync(file.DownloadName,
                stream => _fileSource.DownloadAsync(file, stream, cancellationToken), cancellationToken);
            status = ContentStatus.Stored;
        }

        return new InstructionsContent(ContentOrigin.Drive, file.Id, file.Name, file.DownloadMimeType, file.Revision,
            file.SizeBytes, storedName, status);
    }

    private sealed record CurrentMaterial(ContentFingerprint Fingerprint, string Title, RemoteFile? File);
}
