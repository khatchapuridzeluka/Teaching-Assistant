using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Fetches an assignment's submissions: for each turned-in submission it compares what the source reports now
/// with the latest stored version, and only when something changed downloads the files and records a new version.
/// Every student's state and grades are recorded too, also for students who turned nothing in.
/// Works in two phases: students are prepared in parallel (source calls, downloads, files on disk), then their
/// results are recorded one by one, because the database context must not be used from several threads.
/// </summary>
public class SubmissionFetchService : ISubmissionFetchService
{
    // Larger files are recorded but not downloaded; student homework is far below this.
    private const long MaxFileBytes = 100L * 1024 * 1024;

    // Students prepared at the same time. Most time is spent waiting for Google, so a few at once is much faster.
    private const int MaxParallelStudents = 5;

    // File calls to the source (metadata and downloads) in flight at once, across all students. A student's files
    // are read in parallel too, so this cap keeps a class of many-file submissions within Google's rate limits.
    private const int MaxParallelFileCalls = 8;

    // Classroom states meaning work was handed in. RETURNED = handed in, then returned by the teacher.
    private static readonly HashSet<string> TurnedInStates = ["TURNED_IN", "RETURNED"];

    // Shown in place of a student's name for problems with the assignment's instructions.
    private const string InstructionsLabel = "Instructions";

    private readonly ISubmissionSource _submissionSource;
    private readonly IRemoteFileSource _fileSource;
    private readonly ISnapshotStorage _storage;
    private readonly ISubmissionStore _store;
    private readonly IInstructionsFetchService _instructionsFetch;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _fileCalls = new(MaxParallelFileCalls);

    public SubmissionFetchService(
        ISubmissionSource submissionSource,
        IRemoteFileSource fileSource,
        ISnapshotStorage storage,
        ISubmissionStore store,
        IInstructionsFetchService instructionsFetch,
        TimeProvider timeProvider)
    {
        _submissionSource = submissionSource;
        _fileSource = fileSource;
        _storage = storage;
        _store = store;
        _instructionsFetch = instructionsFetch;
        _timeProvider = timeProvider;
    }

    public async Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        // The submissions, plus the course and the assignment (names for the folders, and the assignment's instructions
        // to store). None of the three depends on another, so they are requested at the same time.
        var listedCall = _submissionSource.GetSubmissionsAsync(courseId, assignmentId, cancellationToken);
        var courseCall = _submissionSource.GetCourseAsync(courseId, cancellationToken);
        var assignmentCall = _submissionSource.GetAssignmentAsync(courseId, assignmentId, cancellationToken);
        await Task.WhenAll(listedCall, courseCall, assignmentCall);
        var (listed, course, assignment) = (await listedCall, await courseCall, await assignmentCall);
        var names = new FolderNames(course.Name, assignment.Title);

        // Instructions first: grading needs them, and they are one small download. A failure here is reported but does
        // not stop the submissions.
        var problems = new List<FetchProblem>();
        var instructions = await FetchInstructionsAsync(courseId, course.Name, assignment, problems, cancellationToken);

        var stored = (await _store.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
            .ToDictionary(s => s.ExternalSubmissionId);
        var turnedIn = listed.Where(s => TurnedInStates.Contains(s.State)).ToList();

        // Phase 1, in parallel: only reads stored submissions, never changes them or touches the database.
        var results = new StudentResult[turnedIn.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, turnedIn.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelStudents, CancellationToken = cancellationToken },
            async (i, token) => results[i] = await PrepareAsync(
                turnedIn[i], stored.GetValueOrDefault(turnedIn[i].Id), courseId, assignmentId, names, token));

        // Phase 2, one by one in list order: apply the results to the submissions and save.
        int newSubmissions = 0, newVersions = 0, unchanged = 0;

        foreach (var result in results)
        {
            problems.AddRange(result.Problems);
            if (result.Submission is not { } submission)
            {
                continue; // skipped; the reason is in the problems
            }

            submission.UpdateStudentName(result.Item.StudentName);

            if (result.Version is not { } version)
            {
                unchanged++;
                if (result.Repair is { } repair)
                {
                    // The files were restored in phase 1; record the rebuilt code on the same version.
                    submission.LatestVersion!.RecordExtraction(repair.ContentHash, repair.ExtractedStoredNames, ExtractionDetail(repair));
                }

                continue;
            }

            // A submission a course sync recorded has no versions yet: its first files make it new, too.
            var first = submission.LatestVersion is null;
            var added = submission.AddVersion(version.Status, version.StatusDetail, contentHash: null, version.StoragePath,
                result.Item.TurnedInAt, version.FetchedAt, version.Contents);
            added.RecordExtraction(version.Extraction.ContentHash, version.Extraction.ExtractedStoredNames,
                ExtractionDetail(version.Extraction));

            if (result.IsNew)
            {
                _store.Add(submission);
            }

            if (first)
            {
                newSubmissions++;
            }
            else
            {
                newVersions++;
            }

            // The version's files are already in place, so record it right away rather than at the end.
            await _store.SaveChangesAsync(cancellationToken);
        }

        // State and grades for every student of the assignment, also those who turned nothing in (missing work counts
        // as 0) and those skipped above: they are recorded without files.
        var now = _timeProvider.GetUtcNow();
        var byId = new Dictionary<string, Submission>(stored);
        foreach (var result in results)
        {
            if (result.Submission is { } prepared)
            {
                byId[result.Item.Id] = prepared;
            }
        }

        foreach (var item in listed)
        {
            if (!byId.TryGetValue(item.Id, out var submission))
            {
                submission = new Submission(SubmissionSource.GoogleClassroom, courseId, assignmentId, item.StudentId,
                    item.StudentName, item.Id,
                    _storage.GetSubmissionFolder(names.CourseName, names.AssignmentTitle, item.StudentName, item.StudentId), now);
                _store.Add(submission);
                byId[item.Id] = submission;
            }

            SubmissionStatus.Record(submission, item, now);
        }

        await _store.SaveChangesAsync(cancellationToken);
        return new FetchSummary(listed.Count, newSubmissions, newVersions, unchanged,
            listed.Count - turnedIn.Count, problems, instructions?.VersionNumber, instructions?.IsNew ?? false);
    }

    // Stores the assignment's instructions when they changed. Null when they could not be read; the previously stored
    // version, if any, stays in use for grading.
    private async Task<InstructionsFetchResult?> FetchInstructionsAsync(
        string courseId, string courseName, AssignmentResponse assignment, List<FetchProblem> problems,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _instructionsFetch.FetchAsync(courseId, courseName, assignment, cancellationToken);
            problems.AddRange(result.Problems.Select(p => new FetchProblem(InstructionsLabel, p)));
            return result;
        }
        catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
        {
            problems.Add(new FetchProblem(InstructionsLabel,
                $"Could not store the assignment's instructions: {ex.Message} Grading uses the previously stored " +
                "instructions, if any; retried on the next fetch."));
            return null;
        }
    }

    // Everything for one student that talks to the source or the disk: read attachments, compare, download.
    private async Task<StudentResult> PrepareAsync(
        SubmissionResponse item, Submission? existing, string courseId, string assignmentId,
        FolderNames? names, CancellationToken cancellationToken)
    {
        var problems = new List<FetchProblem>();

        var current = await DescribeAsync(item, problems, cancellationToken);
        if (current is null)
        {
            return StudentResult.Skipped(item, problems); // attachments could not be read; retried on the next fetch
        }

        if (current.Count == 0)
        {
            problems.Add(new FetchProblem(item.StudentName, "Turned in without attachments."));
            return StudentResult.Skipped(item, problems);
        }

        // Names are always loaded when a submission is new (see FetchAsync).
        var submission = existing ?? new Submission(SubmissionSource.GoogleClassroom, courseId, assignmentId,
            item.StudentId, item.StudentName, item.Id,
            _storage.GetSubmissionFolder(names!.CourseName, names.AssignmentTitle, item.StudentName, item.StudentId),
            _timeProvider.GetUtcNow());

        if (!submission.HasChanged(current.Select(c => c.Fingerprint)))
        {
            // Unchanged at the source, but the database alone does not prove the files are on disk.
            var repair = await RepairLatestAsync(submission, item, current, problems, cancellationToken);
            return new StudentResult(item, submission, existing is null, null, problems, repair);
        }

        var version = await TryStoreFilesAsync(submission, item, current, problems, cancellationToken);
        return version is null
            ? StudentResult.Skipped(item, problems)
            : new StudentResult(item, submission, existing is null, version, problems);
    }

    // Brings the latest version's folder back in line with the database, in place: originals missing on disk are
    // downloaded again under their recorded names (the source is unchanged, so the bytes are the same), and the code
    // folder is rebuilt when originals came back or the code is gone. Not a new version: the student did not resubmit.
    // Only touches the disk; the new extraction is recorded on the version in phase 2. Null when nothing was missing or
    // a download failed (then the problem is reported and the next fetch tries again).
    private async Task<CodeExtraction?> RepairLatestAsync(
        Submission submission, SubmissionResponse item, List<CurrentContent> current,
        List<FetchProblem> problems, CancellationToken cancellationToken)
    {
        if (submission.LatestVersion is not { } version)
        {
            return null;
        }

        var missing = version.Contents
            .Where(c => c.StoredName is not null && !_storage.OriginalExists(version.StoragePath, c.StoredName))
            .ToList();
        var codeMissing = version.ContentHash is not null && _storage.ListCode(version.StoragePath).Count == 0;
        if (missing.Count == 0 && !codeMissing)
        {
            return null;
        }

        try
        {
            foreach (var content in missing)
            {
                var file = current.FirstOrDefault(c => c.Fingerprint.ExternalId == content.ExternalId)?.File;
                if (file is null)
                {
                    continue; // cannot happen while unchanged; nothing to download from
                }

                await _storage.RestoreOriginalAsync(version.StoragePath, content.StoredName!,
                    stream => LimitedAsync(async () =>
                    {
                        await _fileSource.DownloadAsync(file, stream, cancellationToken);
                        return true;
                    }, cancellationToken),
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
        {
            problems.Add(new FetchProblem(item.StudentName,
                $"v{version.Number} has files missing from storage, and downloading them again failed: {ex.Message} " +
                "Retried on the next fetch."));
            return null;
        }

        var extraction = await _storage.ReExtractAsync(version.StoragePath, version.Contents
            .Where(c => c.StoredName is not null)
            .Select(c => new ExtractableFile(c.StoredName!, c.Kind))
            .ToList(), cancellationToken);

        problems.Add(new FetchProblem(item.StudentName, missing.Count > 0
            ? $"v{version.Number}: {string.Join(", ", missing.Select(c => $"'{c.Name}'"))} missing from storage; downloaded again."
            : $"v{version.Number}: extracted code was missing from storage; extracted again."));
        return extraction;
    }

    // What the source reports now for each attachment, read in parallel without downloading. Null when an attachment
    // could not be read: comparing an incomplete list would look like a change and record a version with a file missing.
    private async Task<List<CurrentContent>?> DescribeAsync(
        SubmissionResponse item, List<FetchProblem> problems, CancellationToken cancellationToken)
    {
        var described = await Task.WhenAll(item.Attachments.Select(a => DescribeAttachmentAsync(item, a, cancellationToken)));

        problems.AddRange(described.Select(d => d.Problem).OfType<FetchProblem>());
        if (described.Any(d => d.Failed))
        {
            return null;
        }

        // Task.WhenAll keeps the input order, so contents stay in the order the student attached them.
        return described.Select(d => d.Content).OfType<CurrentContent>().ToList();
    }

    private async Task<DescribedAttachment> DescribeAttachmentAsync(
        SubmissionResponse item, SubmissionAttachment attachment, CancellationToken cancellationToken)
    {
        switch (attachment)
        {
            case { Kind: AttachmentKind.DriveFile, FileId: { } fileId }:
                try
                {
                    var file = await LimitedAsync(() => _fileSource.GetFileAsync(fileId, cancellationToken), cancellationToken);
                    return new DescribedAttachment(
                        new CurrentContent(new ContentFingerprint(ContentOrigin.Drive, file.Id, file.Revision), file), null);
                }
                catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
                {
                    return new DescribedAttachment(null, new FetchProblem(item.StudentName,
                        $"Could not read '{attachment.Title}': {ex.Message} Skipped; retried on the next fetch."), Failed: true);
                }

            case { Kind: AttachmentKind.Link, Url: { } url }:
                // Until repositories are cloned, the URL itself stands in for the revision.
                var origin = IsGitHub(url) ? ContentOrigin.GitHub : ContentOrigin.Link;
                return new DescribedAttachment(
                    new CurrentContent(new ContentFingerprint(origin, url, url), null, attachment.Title), null);

            default:
                return new DescribedAttachment(null,
                    new FetchProblem(item.StudentName, $"'{attachment.Title}' is not a file or link and was ignored."));
        }
    }

    // Downloads the files into the next version's folder. Null when a download failed;
    // then the partial files are discarded and nothing is recorded.
    private async Task<PreparedVersion?> TryStoreFilesAsync(
        Submission submission, SubmissionResponse item, List<CurrentContent> current,
        List<FetchProblem> problems, CancellationToken cancellationToken)
    {
        await using var writer = _storage.BeginVersion(submission.StorageFolder, submission.NextVersionNumber);
        StoredContent[] results;
        CodeExtraction extraction;

        try
        {
            // Files download in parallel; Task.WhenAll waits for all of them (so the writer is never disposed
            // mid-download) and keeps the input order.
            results = await Task.WhenAll(current.Select(content => content.File is { } file
                ? StoreFileAsync(writer, file, cancellationToken)
                : Task.FromResult(LinkContent(content))));

            // Unpack the code next to the originals before committing, so a version appears with both or not at all.
            extraction = await writer.ExtractCodeAsync(results
                .Where(r => r.Content.StoredName is not null)
                .Select(r => new ExtractableFile(r.Content.StoredName!, r.Content.Kind))
                .ToList(), cancellationToken);

            await writer.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
        {
            problems.Add(new FetchProblem(item.StudentName, $"Download failed: {ex.Message} Skipped; retried on the next fetch."));
            return null;
        }

        var contents = results.Select(r => r.Content).ToList();
        var notStored = results.Select(r => r.NotStoredReason).OfType<string>().ToList();
        var storedCount = contents.Count(c => c.StoredName is not null);
        var status = notStored.Count == 0 ? VersionStatus.Ready
            : storedCount > 0 ? VersionStatus.PartiallySupported
            : VersionStatus.Failed;
        var detail = notStored.Count == 0 ? null : string.Join(" ", notStored);
        if (detail is not null)
        {
            problems.Add(new FetchProblem(item.StudentName, detail));
        }

        problems.AddRange(extraction.Problems.Select(p => new FetchProblem(item.StudentName, p)));

        return new PreparedVersion(status, detail, writer.RelativePath, _timeProvider.GetUtcNow(), contents, extraction);
    }

    private async Task<StoredContent> StoreFileAsync(ISnapshotWriter writer, RemoteFile file, CancellationToken cancellationToken)
    {
        string? storedName = null;
        string? notStoredReason = null;
        ContentStatus status;

        if (!file.IsDownloadable)
        {
            notStoredReason = $"'{file.Name}' ({file.MimeType}) cannot be downloaded.";
            status = ContentStatus.Unsupported;
        }
        else if (file.SizeBytes > MaxFileBytes)
        {
            notStoredReason = $"'{file.Name}' is larger than {MaxFileBytes / 1024 / 1024} MB.";
            status = ContentStatus.TooLarge;
        }
        else
        {
            storedName = await writer.AddOriginalAsync(file.DownloadName,
                stream => LimitedAsync(async () =>
                {
                    await _fileSource.DownloadAsync(file, stream, cancellationToken);
                    return true;
                }, cancellationToken),
                cancellationToken);
            status = ContentStatus.Stored;
        }

        var content = new VersionContent(ContentOrigin.Drive, ContentClassifier.Classify(file.DownloadName, file.DownloadMimeType),
            file.Id, file.Name, file.DownloadMimeType, file.Revision, null, file.SizeBytes, storedName, null, status);
        return new StoredContent(content, notStoredReason);
    }

    private static StoredContent LinkContent(CurrentContent content)
    {
        var url = content.Fingerprint.ExternalId;
        var isRepository = content.Fingerprint.Origin == ContentOrigin.GitHub;

        var versionContent = new VersionContent(content.Fingerprint.Origin, isRepository ? ContentKind.Repository : ContentKind.Other,
            url, content.Title ?? url, null, content.Fingerprint.Fingerprint, null, null, null, null, ContentStatus.Unsupported);
        return new StoredContent(versionContent,
            isRepository ? $"GitHub repository {url} is not fetched yet." : $"Link {url} cannot be downloaded.");
    }

    internal static string? ExtractionDetail(CodeExtraction extraction) =>
        extraction.Problems.Count == 0 ? null : string.Join(" ", extraction.Problems);

    // Runs one call to the source under the shared cap on parallel file calls.
    private async Task<T> LimitedAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        await _fileCalls.WaitAsync(cancellationToken);
        try
        {
            return await call();
        }
        finally
        {
            _fileCalls.Release();
        }
    }

    private static bool IsGitHub(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase));

    // Course and assignment names used for the folders of new submissions.
    private sealed record FolderNames(string CourseName, string AssignmentTitle);

    // One attachment as the source reports it now: a Drive file (File set) or a link (Title set).
    private sealed record CurrentContent(ContentFingerprint Fingerprint, RemoteFile? File, string? Title = null);

    // The result of reading one attachment. Failed means it could not be read, so the student is skipped.
    private sealed record DescribedAttachment(CurrentContent? Content, FetchProblem? Problem, bool Failed = false);

    // One stored (or deliberately not stored) piece of content, with the reason when it was not stored.
    private sealed record StoredContent(VersionContent Content, string? NotStoredReason);

    // A new version whose files are already stored, waiting to be added to its submission in phase 2.
    private sealed record PreparedVersion(
        VersionStatus Status, string? StatusDetail, string StoragePath, DateTimeOffset FetchedAt,
        List<VersionContent> Contents, CodeExtraction Extraction);

    // The outcome of phase 1 for one student. Submission is null when the student was skipped;
    // Version is null when nothing changed. Repair is set when the unchanged latest version's files were restored.
    private sealed record StudentResult(
        SubmissionResponse Item, Submission? Submission, bool IsNew, PreparedVersion? Version, List<FetchProblem> Problems,
        CodeExtraction? Repair = null)
    {
        public static StudentResult Skipped(SubmissionResponse item, List<FetchProblem> problems) =>
            new(item, null, false, null, problems);
    }
}
