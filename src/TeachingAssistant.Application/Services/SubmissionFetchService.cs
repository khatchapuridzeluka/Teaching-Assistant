using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class SubmissionFetchService : ISubmissionFetchService
{
    private const long MaxFileBytes = 100L * 1024 * 1024;

    private const int MaxParallelStudents = 5;

    private const int MaxParallelFileCalls = 8;

    private static readonly HashSet<string> TurnedInStates = ["TURNED_IN", "RETURNED"];

    private const string InstructionsLabel = "Instructions";

    private readonly ISubmissionSource _submissionSource;
    private readonly IRemoteFileSource _fileSource;
    private readonly IRepositorySource _repositorySource;
    private readonly ISnapshotStorage _storage;
    private readonly ISubmissionStore _store;
    private readonly IInstructionsFetchService _instructionsFetch;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _fileCalls = new(MaxParallelFileCalls);

    public SubmissionFetchService(
        ISubmissionSource submissionSource,
        IRemoteFileSource fileSource,
        IRepositorySource repositorySource,
        ISnapshotStorage storage,
        ISubmissionStore store,
        IInstructionsFetchService instructionsFetch,
        TimeProvider timeProvider)
    {
        _submissionSource = submissionSource;
        _fileSource = fileSource;
        _repositorySource = repositorySource;
        _storage = storage;
        _store = store;
        _instructionsFetch = instructionsFetch;
        _timeProvider = timeProvider;
    }

    public async Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var listedCall = _submissionSource.GetSubmissionsAsync(courseId, assignmentId, cancellationToken);
        var courseCall = _submissionSource.GetCourseAsync(courseId, cancellationToken);
        var assignmentCall = _submissionSource.GetAssignmentAsync(courseId, assignmentId, cancellationToken);
        await Task.WhenAll(listedCall, courseCall, assignmentCall);
        var (listed, course, assignment) = (await listedCall, await courseCall, await assignmentCall);
        var names = new FolderNames { CourseName = course.Name, AssignmentTitle = assignment.Title };

        var problems = new List<FetchProblem>();
        var instructions = await FetchInstructionsAsync(courseId, course.Name, assignment, problems, cancellationToken);

        var stored = (await _store.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
            .ToDictionary(s => s.ExternalSubmissionId);
        var turnedIn = listed.Where(s => TurnedInStates.Contains(s.State)).ToList();

        var results = new StudentResult[turnedIn.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, turnedIn.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelStudents, CancellationToken = cancellationToken },
            async (i, token) => results[i] = await PrepareAsync(
                turnedIn[i], stored.GetValueOrDefault(turnedIn[i].Id), courseId, assignmentId, names, token));

        int newSubmissions = 0, newVersions = 0, unchanged = 0;

        foreach (var result in results)
        {
            problems.AddRange(result.Problems);
            if (result.Submission is not { } submission)
            {
                continue;
            }

            submission.UpdateStudentName(result.Item.StudentName);

            if (result.Version is not { } version)
            {
                unchanged++;
                if (result.Repair is { } repair)
                {
                    submission.LatestVersion!.RecordExtraction(repair.ContentHash, repair.ExtractedStoredNames, ExtractionDetail(repair));
                }

                continue;
            }

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

            await _store.SaveChangesAsync(cancellationToken);
        }

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
        return new FetchSummary
        {
            Submissions = listed.Count,
            NewSubmissions = newSubmissions,
            NewVersions = newVersions,
            Unchanged = unchanged,
            NotSubmitted = listed.Count - turnedIn.Count,
            Problems = problems,
            InstructionsVersion = instructions?.VersionNumber,
            NewInstructions = instructions?.IsNew ?? false,
        };
    }

    private async Task<InstructionsFetchResult?> FetchInstructionsAsync(
        string courseId, string courseName, AssignmentResponse assignment, List<FetchProblem> problems,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _instructionsFetch.FetchAsync(courseId, courseName, assignment, cancellationToken);
            problems.AddRange(result.Problems.Select(p => new FetchProblem { StudentName = InstructionsLabel, Detail = p }));
            return result;
        }
        catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
        {
            problems.Add(new FetchProblem
            {
                StudentName = InstructionsLabel,
                Detail = $"Could not store the assignment's instructions: {ex.Message} Grading uses the previously stored " +
                "instructions, if any; retried on the next fetch.",
            });
            return null;
        }
    }

    private async Task<StudentResult> PrepareAsync(
        SubmissionResponse item, Submission? existing, string courseId, string assignmentId,
        FolderNames? names, CancellationToken cancellationToken)
    {
        var problems = new List<FetchProblem>();

        var current = await DescribeAsync(item, problems, cancellationToken);
        if (current is null)
        {
            return StudentResult.Skipped(item, problems);
        }

        if (current.Count == 0)
        {
            problems.Add(new FetchProblem { StudentName = item.StudentName, Detail = "Turned in without attachments." });
            return StudentResult.Skipped(item, problems);
        }

        var submission = existing ?? new Submission(SubmissionSource.GoogleClassroom, courseId, assignmentId,
            item.StudentId, item.StudentName, item.Id,
            _storage.GetSubmissionFolder(names!.CourseName, names.AssignmentTitle, item.StudentName, item.StudentId),
            _timeProvider.GetUtcNow());

        if (!submission.HasChanged(current.Select(c => c.Fingerprint)))
        {
            var repair = await RepairLatestAsync(submission, item, current, problems, cancellationToken);
            return new StudentResult
            {
                Item = item,
                Submission = submission,
                IsNew = existing is null,
                Problems = problems,
                Repair = repair,
            };
        }

        var version = await TryStoreFilesAsync(submission, item, current, problems, cancellationToken);
        return version is null
            ? StudentResult.Skipped(item, problems)
            : new StudentResult
            {
                Item = item,
                Submission = submission,
                IsNew = existing is null,
                Version = version,
                Problems = problems,
            };
    }

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
                var source = current.FirstOrDefault(c => c.Fingerprint.ExternalId == content.ExternalId);
                if (source?.File is { } file)
                {
                    await _storage.RestoreOriginalAsync(version.StoragePath, content.StoredName!,
                        stream => LimitedAsync(async () =>
                        {
                            await _fileSource.DownloadAsync(file, stream, cancellationToken);
                            return true;
                        }, cancellationToken),
                        cancellationToken);
                }
                else if (source?.Repository is { } repository)
                {
                    await _storage.RestoreOriginalAsync(version.StoragePath, content.StoredName!,
                        stream => DownloadRepositoryAsync(repository, stream, cancellationToken), cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
        {
            problems.Add(new FetchProblem
            {
                StudentName = item.StudentName,
                Detail = $"v{version.Number} has files missing from storage, and downloading them again failed: {ex.Message} " +
                "Retried on the next fetch.",
            });
            return null;
        }

        var extraction = await _storage.ReExtractAsync(version.StoragePath, version.Contents
            .Where(c => c.StoredName is not null)
            .Select(c => new ExtractableFile { StoredName = c.StoredName!, Kind = c.Kind })
            .ToList(), cancellationToken);

        problems.Add(new FetchProblem
        {
            StudentName = item.StudentName,
            Detail = missing.Count > 0
                ? $"v{version.Number}: {string.Join(", ", missing.Select(c => $"'{c.Name}'"))} missing from storage; downloaded again."
                : $"v{version.Number}: extracted code was missing from storage; extracted again.",
        });
        return extraction;
    }

    private async Task<List<CurrentContent>?> DescribeAsync(
        SubmissionResponse item, List<FetchProblem> problems, CancellationToken cancellationToken)
    {
        var described = await Task.WhenAll(item.Attachments.Select(a => DescribeAttachmentAsync(item, a, cancellationToken)));

        problems.AddRange(described.Select(d => d.Problem).OfType<FetchProblem>());
        if (described.Any(d => d.Failed))
        {
            return null;
        }

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
                    return new DescribedAttachment
                    {
                        Content = new CurrentContent
                        {
                            Fingerprint = new ContentFingerprint
                            {
                                Origin = ContentOrigin.Drive,
                                ExternalId = file.Id,
                                Fingerprint = file.Revision,
                            },
                            File = file,
                        },
                    };
                }
                catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
                {
                    return new DescribedAttachment
                    {
                        Problem = new FetchProblem
                        {
                            StudentName = item.StudentName,
                            Detail = $"Could not read '{attachment.Title}': {ex.Message} Skipped; retried on the next fetch.",
                        },
                        Failed = true,
                    };
                }

            case { Kind: AttachmentKind.Link, Url: { } url } when IsGitHub(url):
                try
                {
                    var repository = await LimitedAsync(() => _repositorySource.ResolveAsync(url, cancellationToken), cancellationToken);
                    return new DescribedAttachment
                    {
                        Content = new CurrentContent
                        {
                            Fingerprint = new ContentFingerprint
                            {
                                Origin = ContentOrigin.GitHub,
                                ExternalId = url,
                                Fingerprint = repository.CommitSha,
                            },
                            Title = attachment.Title,
                            Repository = repository,
                        },
                    };
                }
                catch (SubmissionSourceNotFoundException ex)
                {
                    // Private, renamed or deleted: recorded with the reason, so the teacher sees it; a later fetch picks the
                    // code up once the student makes the repository public.
                    return new DescribedAttachment
                    {
                        Content = new CurrentContent
                        {
                            Fingerprint = new ContentFingerprint { Origin = ContentOrigin.GitHub, ExternalId = url, Fingerprint = url },
                            Title = attachment.Title,
                            NotFoundReason = ex.Message,
                        },
                    };
                }
                catch (SubmissionSourceUnavailableException ex)
                {
                    return new DescribedAttachment
                    {
                        Problem = new FetchProblem
                        {
                            StudentName = item.StudentName,
                            Detail = $"Could not read '{attachment.Title}': {ex.Message} Skipped; retried on the next fetch.",
                        },
                        Failed = true,
                    };
                }

            case { Kind: AttachmentKind.Link, Url: { } url }:
                return new DescribedAttachment
                {
                    Content = new CurrentContent
                    {
                        Fingerprint = new ContentFingerprint { Origin = ContentOrigin.Link, ExternalId = url, Fingerprint = url },
                        Title = attachment.Title,
                    },
                };

            default:
                return new DescribedAttachment
                {
                    Problem = new FetchProblem
                    {
                        StudentName = item.StudentName,
                        Detail = $"'{attachment.Title}' is not a file or link and was ignored.",
                    },
                };
        }
    }

    private async Task<PreparedVersion?> TryStoreFilesAsync(
        Submission submission, SubmissionResponse item, List<CurrentContent> current,
        List<FetchProblem> problems, CancellationToken cancellationToken)
    {
        await using var writer = _storage.BeginVersion(submission.StorageFolder, submission.NextVersionNumber);
        StoredContent[] results;
        CodeExtraction extraction;

        try
        {
            results = await Task.WhenAll(current.Select(content => content switch
            {
                { File: { } file } => StoreFileAsync(writer, file, cancellationToken),
                { Repository: { } repository } => StoreRepositoryAsync(writer, content, repository, cancellationToken),
                _ => Task.FromResult(LinkContent(content)),
            }));

            extraction = await writer.ExtractCodeAsync(results
                .Where(r => r.Content.StoredName is not null)
                .Select(r => new ExtractableFile { StoredName = r.Content.StoredName!, Kind = r.Content.Kind })
                .ToList(), cancellationToken);

            await writer.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is SubmissionSourceNotFoundException or SubmissionSourceUnavailableException)
        {
            problems.Add(new FetchProblem
            {
                StudentName = item.StudentName,
                Detail = $"Download failed: {ex.Message} Skipped; retried on the next fetch.",
            });
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
            problems.Add(new FetchProblem { StudentName = item.StudentName, Detail = detail });
        }

        problems.AddRange(extraction.Problems.Select(p => new FetchProblem { StudentName = item.StudentName, Detail = p }));

        return new PreparedVersion
        {
            Status = status,
            StatusDetail = detail,
            StoragePath = writer.RelativePath,
            FetchedAt = _timeProvider.GetUtcNow(),
            Contents = contents,
            Extraction = extraction,
        };
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
        return new StoredContent { Content = content, NotStoredReason = notStoredReason };
    }

    private async Task<StoredContent> StoreRepositoryAsync(
        ISnapshotWriter writer, CurrentContent content, RepositorySnapshot repository, CancellationToken cancellationToken)
    {
        var storedName = await writer.AddOriginalAsync(repository.DownloadName,
            stream => DownloadRepositoryAsync(repository, stream, cancellationToken), cancellationToken);
        var url = content.Fingerprint.ExternalId;
        var versionContent = new VersionContent(ContentOrigin.GitHub, ContentKind.Repository, url, content.Title ?? repository.FullName,
            "application/zip", repository.CommitSha, repository.Ref, null, storedName, null, ContentStatus.Stored);
        return new StoredContent { Content = versionContent };
    }

    private Task<bool> DownloadRepositoryAsync(RepositorySnapshot repository, Stream stream, CancellationToken cancellationToken)
    {
        return LimitedAsync(async () =>
        {
            await _repositorySource.DownloadAsync(repository, stream, cancellationToken);
            return true;
        }, cancellationToken);
    }

    private static StoredContent LinkContent(CurrentContent content)
    {
        var url = content.Fingerprint.ExternalId;
        var isRepository = content.Fingerprint.Origin == ContentOrigin.GitHub;

        var versionContent = new VersionContent(content.Fingerprint.Origin, isRepository ? ContentKind.Repository : ContentKind.Other,
            url, content.Title ?? url, null, content.Fingerprint.Fingerprint, null, null, null, null, ContentStatus.Unsupported);
        return new StoredContent
        {
            Content = versionContent,
            NotStoredReason = isRepository ? content.NotFoundReason ?? $"GitHub repository {url} could not be fetched." : $"Link {url} cannot be downloaded.",
        };
    }

    internal static string? ExtractionDetail(CodeExtraction extraction) =>
        extraction.Problems.Count == 0 ? null : string.Join(" ", extraction.Problems);

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

    private sealed record FolderNames
    {
        public required string CourseName { get; init; }
        public required string AssignmentTitle { get; init; }
    }

    private sealed record CurrentContent
    {
        public required ContentFingerprint Fingerprint { get; init; }
        public RemoteFile? File { get; init; }
        public string? Title { get; init; }
        public RepositorySnapshot? Repository { get; init; }
        public string? NotFoundReason { get; init; }
    }

    private sealed record DescribedAttachment
    {
        public CurrentContent? Content { get; init; }
        public FetchProblem? Problem { get; init; }
        public bool Failed { get; init; }
    }

    private sealed record StoredContent
    {
        public required VersionContent Content { get; init; }
        public string? NotStoredReason { get; init; }
    }

    private sealed record PreparedVersion
    {
        public required VersionStatus Status { get; init; }
        public string? StatusDetail { get; init; }
        public required string StoragePath { get; init; }
        public required DateTimeOffset FetchedAt { get; init; }
        public required List<VersionContent> Contents { get; init; }
        public required CodeExtraction Extraction { get; init; }
    }

    private sealed record StudentResult
    {
        public required SubmissionResponse Item { get; init; }
        public Submission? Submission { get; init; }
        public required bool IsNew { get; init; }
        public PreparedVersion? Version { get; init; }
        public required List<FetchProblem> Problems { get; init; }
        public CodeExtraction? Repair { get; init; }

        public static StudentResult Skipped(SubmissionResponse item, List<FetchProblem> problems) =>
            new() { Item = item, IsNew = false, Problems = problems };
    }
}
