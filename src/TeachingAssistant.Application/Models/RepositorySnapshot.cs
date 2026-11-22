namespace TeachingAssistant.Application.Models;

/// <summary>
/// The commit a repository link points to at fetch time. The commit SHA identifies the version: a new push gives a new SHA,
/// and so a new version.
/// </summary>
public record RepositorySnapshot
{
    public required string Owner { get; init; }
    public required string Repository { get; init; }
    public required string CommitSha { get; init; }
    public required string Ref { get; init; }

    public string FullName => $"{Owner}/{Repository}";

    public string DownloadName => $"{Repository}-{CommitSha[..Math.Min(7, CommitSha.Length)]}.zip";
}
