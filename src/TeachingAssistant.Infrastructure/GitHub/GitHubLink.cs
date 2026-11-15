namespace TeachingAssistant.Infrastructure.GitHub;

public enum GitHubLinkKind
{
    Repository,

    Branch,

    PullRequest,

    Commit,
}

/// <summary>
/// A GitHub link as students paste it: the repository, a branch or folder (tree/... or blob/...), a pull request or a commit.
/// For a branch link the whole repository at that branch is fetched, not only the folder.
/// </summary>
public sealed record GitHubLink(string Owner, string Repository, GitHubLinkKind Kind, string? Ref)
{
    public static GitHubLink? TryParse(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
        if (segments.Length < 2)
        {
            return null;
        }

        var owner = segments[0];
        var repository = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        if (owner.Length == 0 || repository.Length == 0)
        {
            return null;
        }

        var section = segments.Length > 2 ? segments[2].ToLowerInvariant() : null;
        var value = segments.Length > 3 ? segments[3] : null;
        return section switch
        {
            "pull" when int.TryParse(value, out _) => new GitHubLink(owner, repository, GitHubLinkKind.PullRequest, value),
            "commit" when value is not null => new GitHubLink(owner, repository, GitHubLinkKind.Commit, value),
            "tree" or "blob" when value is not null => new GitHubLink(owner, repository, GitHubLinkKind.Branch, value),
            _ => new GitHubLink(owner, repository, GitHubLinkKind.Repository, null),
        };
    }
}
