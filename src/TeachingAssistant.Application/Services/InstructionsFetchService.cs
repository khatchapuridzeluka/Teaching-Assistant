using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Stores an assignment's instructions once per change. It compares what the source reports now (title, task statement,
/// maximum points, and each attached file's revision, read without downloading) with the latest stored version, and only
/// when something differs downloads the files into a new version. Only files the grader can use are downloaded: PDFs,
/// images and documents it can read as text (Word); links and videos are recorded but not stored.
/// </summary>
public class InstructionsFetchService : IInstructionsFetchService
{
    // Instruction files are a few pages; anything larger is not meant for grading.
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
            return new InstructionsFetchResult(latest.Number, IsNew: false, []);
        }

        var instructions = existing ?? new AssignmentInstructions(SubmissionSource.GoogleClassroom, courseId, assignment.Id,
            _storage.GetInstructionsFolder(courseName, assignment.Title, assignment.Id), _timeProvider.GetUtcNow());

        var problems = new List<string>();
        var contents = new List<InstructionsContent>();

        // Files go to a private folder first and appear all at once on commit; a failed download leaves nothing behind.
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

    // What each material is right now, read without downloading: a Drive file's revision, or the link itself.
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
                // Links, videos and forms: nothing to download; the URL (or the title, when there is none) identifies them.
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
            status = ContentStatus.Unsupported; // e.g. a video: kept as a record, not used for grading
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

    // One material as the source reports it now. File is set for Drive files.
    private sealed record CurrentMaterial(ContentFingerprint Fingerprint, string Title, RemoteFile? File);
}
