using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Infrastructure.GitHub;

/// <summary>
/// Reads public GitHub repositories through the REST API, read-only: one call to find the commit a link points to, one to
/// download that commit as a zip. Works without a token (about 60 requests an hour); a read-only token raises the limit.
/// </summary>
public class GitHubRepositorySource : IRepositorySource
{
    private const long MaxDownloadBytes = 100L * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly ILogger<GitHubRepositorySource> _logger;

    public GitHubRepositorySource(HttpClient http, IOptions<GitHubOptions> options, ILogger<GitHubRepositorySource> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.Value.ApiBaseUrl);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TeachingAssistant", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(options.Value.Token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Token.Trim());
        }
    }

    public async Task<RepositorySnapshot> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        var link = GitHubLink.TryParse(url)
            ?? throw new SubmissionSourceNotFoundException($"'{url}' is not a link to a GitHub repository.");
        var name = $"{link.Owner}/{link.Repository}";

        switch (link.Kind)
        {
            case GitHubLinkKind.PullRequest:
            {
                using var pull = await GetJsonAsync($"repos/{link.Owner}/{link.Repository}/pulls/{link.Ref}", name, cancellationToken);
                var head = pull.RootElement.GetProperty("head");
                var sha = head.GetProperty("sha").GetString()!;

                // A pull request from a fork: its commits live in the fork, so the code is downloaded from there.
                var (owner, repository) = head.TryGetProperty("repo", out var repo) && repo.ValueKind == JsonValueKind.Object
                    ? (repo.GetProperty("owner").GetProperty("login").GetString()!, repo.GetProperty("name").GetString()!)
                    : (link.Owner, link.Repository);
                return new RepositorySnapshot
                {
                    Owner = owner,
                    Repository = repository,
                    CommitSha = sha,
                    Ref = $"pull/{link.Ref}",
                };
            }

            default:
            {
                var reference = link.Ref ?? "HEAD";
                using var commit = await GetJsonAsync(
                    $"repos/{link.Owner}/{link.Repository}/commits/{Uri.EscapeDataString(reference)}", name, cancellationToken);
                var sha = commit.RootElement.GetProperty("sha").GetString()!;
                return new RepositorySnapshot
                {
                    Owner = link.Owner,
                    Repository = link.Repository,
                    CommitSha = sha,
                    Ref = link.Ref ?? "default branch",
                };
            }
        }
    }

    public async Task DownloadAsync(RepositorySnapshot snapshot, Stream destination, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(
                $"repos/{snapshot.Owner}/{snapshot.Repository}/zipball/{snapshot.CommitSha}", HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            EnsureSuccess(response, snapshot.FullName);
            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                throw TooLarge(snapshot);
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > MaxDownloadBytes)
                {
                    throw TooLarge(snapshot);
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            throw Unavailable(snapshot.FullName, $"the download failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable(snapshot.FullName, "the download timed out.", ex);
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string path, string repository, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(path, cancellationToken);
            EnsureSuccess(response, repository);
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw Unavailable(repository, $"GitHub could not be reached: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable(repository, "GitHub did not answer in time.", ex);
        }
        catch (JsonException ex)
        {
            throw Unavailable(repository, "GitHub sent an answer that could not be read.", ex);
        }
    }

    private void EnsureSuccess(HttpResponseMessage response, string repository)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            throw new SubmissionSourceNotFoundException(
                $"GitHub repository {repository} (or the branch, pull request or commit in the link) was not found: it is private, "
                + "renamed or deleted. Only public repositories can be fetched; ask the student to make it public.");
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            && response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.FirstOrDefault() == "0")
        {
            var reset = response.Headers.TryGetValues("x-ratelimit-reset", out var values)
                && long.TryParse(values.FirstOrDefault(), out var seconds)
                    ? $" It resets at {DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)}."
                    : "";
            throw Unavailable(repository, $"GitHub's request limit is used up.{reset} A read-only token (GitHub:Token) raises it.");
        }

        throw Unavailable(repository, $"GitHub answered {(int)response.StatusCode}.");
    }

    private SubmissionSourceUnavailableException TooLarge(RepositorySnapshot snapshot)
    {
        return Unavailable(snapshot.FullName, $"the repository is larger than {MaxDownloadBytes / 1024 / 1024} MB.");
    }

    private SubmissionSourceUnavailableException Unavailable(string repository, string reason, Exception? inner = null)
    {
        _logger.LogWarning("GitHub repository {Repository} not fetched: {Reason}", repository, reason);
        return new SubmissionSourceUnavailableException($"Could not fetch GitHub repository {repository}: {reason}", inner);
    }
}
