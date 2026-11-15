using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Reads code from a hosted repository (GitHub), read-only. Throws SubmissionSourceNotFoundException when the link is not
/// a repository or the repository cannot be seen (private, renamed or deleted), and SubmissionSourceUnavailableException
/// when the host cannot answer now (rate limit, network, too large).
/// </summary>
public interface IRepositorySource
{
    /// <summary>Which commit the link points to now: a repository's default branch, a branch, a pull request's latest commit, or a commit.</summary>
    Task<RepositorySnapshot> ResolveAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Writes the code of that commit to the stream as a zip archive.</summary>
    Task DownloadAsync(RepositorySnapshot snapshot, Stream destination, CancellationToken cancellationToken = default);
}
