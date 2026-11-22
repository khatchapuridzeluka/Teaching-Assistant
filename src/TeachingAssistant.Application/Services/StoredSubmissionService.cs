using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class StoredSubmissionService : IStoredSubmissionService
{
    private const string FallbackContentType = "application/octet-stream";

    private readonly ISubmissionStore _store;
    private readonly ISnapshotStorage _storage;

    public StoredSubmissionService(ISubmissionStore store, ISnapshotStorage storage)
    {
        _store = store;
        _storage = storage;
    }

    public async Task<IReadOnlyList<StoredSubmissionResponse>> GetByAssignmentAsync(
        string assignmentId, CancellationToken cancellationToken = default)
    {
        var submissions = await _store.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken);

        return submissions
            .Where(s => s.Versions.Count > 0)
            .OrderBy(s => s.StudentName)
            .Select(s => new StoredSubmissionResponse
            {
                Id = s.Id,
                ExternalSubmissionId = s.ExternalSubmissionId,
                StudentName = s.StudentName,
                Versions = s.Versions.OrderByDescending(v => v.Number).Select(v => new StoredVersionResponse
                {
                    Id = v.Id,
                    Number = v.Number,
                    Status = v.Status,
                    StatusDetail = v.StatusDetail,
                    HasCode = v.ContentHash is not null,
                    ExtractionDetail = v.ExtractionDetail,
                    FetchedAt = v.FetchedAt,
                    TurnedInAt = v.TurnedInAt,
                    Files = v.Contents.Select(c => new StoredFileResponse
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Kind = c.Kind,
                        MimeType = c.MimeType,
                        SizeBytes = c.SizeBytes,
                        Status = c.Status,
                        Stored = c.StoredName is not null,
                    }).ToList(),
                })
                    .ToList(),
            })
            .ToList();
    }

    public async Task<StoredFileContent?> OpenFileAsync(Guid contentId, CancellationToken cancellationToken = default)
    {
        var location = await _store.FindStoredFileAsync(contentId, cancellationToken);
        if (location is null)
        {
            return null;
        }

        var stream = _storage.OpenOriginal(location.VersionPath, location.StoredName);
        return stream is null
            ? null
            : new StoredFileContent
            {
                Content = stream,
                ContentType = location.MimeType ?? FallbackContentType,
                FileName = location.Name,
            };
    }

    public async Task<ExtractionSummary> ReExtractAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        var submissions = await _store.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken);
        var problems = new List<FetchProblem>();
        int versions = 0, withCode = 0;

        foreach (var submission in submissions)
        {
            foreach (var version in submission.Versions)
            {
                var files = version.Contents
                    .Where(c => c.StoredName is not null)
                    .Select(c => new ExtractableFile { StoredName = c.StoredName!, Kind = c.Kind })
                    .ToList();

                var extraction = await _storage.ReExtractAsync(version.StoragePath, files, cancellationToken);
                version.RecordExtraction(extraction.ContentHash, extraction.ExtractedStoredNames,
                    SubmissionFetchService.ExtractionDetail(extraction));

                versions++;
                if (extraction.ContentHash is not null)
                {
                    withCode++;
                }

                problems.AddRange(extraction.Problems.Select(p => new FetchProblem
                {
                    StudentName = submission.StudentName,
                    Detail = $"v{version.Number}: {p}",
                }));
            }
        }

        await _store.SaveChangesAsync(cancellationToken);
        return new ExtractionSummary { Versions = versions, VersionsWithCode = withCode, Problems = problems };
    }

    public async Task<IReadOnlyList<CodeFileResponse>?> ListCodeAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var versionPath = await _store.FindVersionPathAsync(versionId, cancellationToken);
        return versionPath is null
            ? null
            : _storage.ListCode(versionPath).Select(f => new CodeFileResponse { Path = f.Path, SizeBytes = f.SizeBytes }).ToList();
    }

    public async Task<Stream?> OpenCodeFileAsync(Guid versionId, string path, CancellationToken cancellationToken = default)
    {
        var versionPath = await _store.FindVersionPathAsync(versionId, cancellationToken);
        return versionPath is null ? null : _storage.OpenCode(versionPath, path);
    }
}
