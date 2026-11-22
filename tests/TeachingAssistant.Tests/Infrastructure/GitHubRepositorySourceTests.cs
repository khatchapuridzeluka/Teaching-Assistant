using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Infrastructure.GitHub;

namespace TeachingAssistant.Tests.Infrastructure;

public class GitHubRepositorySourceTests
{
    private readonly FakeGitHub _github = new();

    private GitHubRepositorySource Source(string? token = null)
    {
        return new GitHubRepositorySource(new HttpClient(_github), Options.Create(new GitHubOptions { Token = token }),
            NullLogger<GitHubRepositorySource>.Instance);
    }

    [Theory]
    [InlineData("https://github.com/ana/library", GitHubLinkKind.Repository, null)]
    [InlineData("https://github.com/ana/library.git", GitHubLinkKind.Repository, null)]
    [InlineData("https://www.github.com/ana/library/", GitHubLinkKind.Repository, null)]
    [InlineData("https://github.com/ana/library/tree/feature-1/src/main", GitHubLinkKind.Branch, "feature-1")]
    [InlineData("https://github.com/ana/library/blob/main/Main.java", GitHubLinkKind.Branch, "main")]
    [InlineData("https://github.com/ana/library/pull/12", GitHubLinkKind.PullRequest, "12")]
    [InlineData("https://github.com/ana/library/pull/12/files", GitHubLinkKind.PullRequest, "12")]
    [InlineData("https://github.com/ana/library/commit/abc123", GitHubLinkKind.Commit, "abc123")]
    [InlineData("https://github.com/ana/library/issues/3", GitHubLinkKind.Repository, null)]
    public void Links_AreReadAsRepositoryBranchPullRequestOrCommit(string url, GitHubLinkKind kind, string? reference)
    {
        var link = GitHubLink.TryParse(url)!;

        Assert.Equal(("ana", "library", kind, reference), (link.Owner, link.Repository, link.Kind, link.Ref));
    }

    [Theory]
    [InlineData("https://github.com/ana")]
    [InlineData("https://gist.github.com/ana/123")]
    [InlineData("https://gitlab.com/ana/library")]
    [InlineData("not a link")]
    public void NonRepositoryLinks_AreNotRead(string url)
    {
        Assert.Null(GitHubLink.TryParse(url));
    }

    [Fact]
    public async Task ARepositoryLink_ResolvesToTheLatestCommitOfTheDefaultBranch()
    {
        _github.Json["/repos/ana/library/commits/HEAD"] = """{ "sha": "1111111aaaa" }""";

        var snapshot = await Source().ResolveAsync("https://github.com/ana/library", TestContext.Current.CancellationToken);

        Assert.Equal(new RepositorySnapshot
        {
            Owner = "ana",
            Repository = "library",
            CommitSha = "1111111aaaa",
            Ref = "default branch",
        }, snapshot);
        Assert.Equal("library-1111111.zip", snapshot.DownloadName);
        Assert.Null(_github.Authorization);
        Assert.Contains("TeachingAssistant", _github.UserAgent);
    }

    [Fact]
    public async Task APullRequestLink_ResolvesToItsLatestCommit_InTheForkItComesFrom()
    {
        _github.Json["/repos/teacher/course/pulls/12"] = """
            { "head": { "sha": "2222222bbbb", "repo": { "name": "course-fork", "owner": { "login": "ana" } } } }
            """;

        var snapshot = await Source("token-1").ResolveAsync("https://github.com/teacher/course/pull/12", TestContext.Current.CancellationToken);

        Assert.Equal(new RepositorySnapshot
        {
            Owner = "ana",
            Repository = "course-fork",
            CommitSha = "2222222bbbb",
            Ref = "pull/12",
        }, snapshot);
        Assert.Equal("Bearer token-1", _github.Authorization);
    }

    [Fact]
    public async Task APrivateOrMissingRepository_IsNotFound_WithAReasonForTheTeacher()
    {
        var error = await Assert.ThrowsAsync<SubmissionSourceNotFoundException>(() =>
            Source().ResolveAsync("https://github.com/ana/secret", TestContext.Current.CancellationToken));

        Assert.Contains("ana/secret", error.Message);
        Assert.Contains("make it public", error.Message);
    }

    [Fact]
    public async Task AUsedUpRequestLimit_IsUnavailable_AndSuggestsAToken()
    {
        _github.RateLimited = true;

        var error = await Assert.ThrowsAsync<SubmissionSourceUnavailableException>(() =>
            Source().ResolveAsync("https://github.com/ana/library", TestContext.Current.CancellationToken));

        Assert.Contains("request limit is used up", error.Message);
        Assert.Contains("GitHub:Token", error.Message);
    }

    [Fact]
    public async Task Download_WritesTheZipOfThatCommit()
    {
        _github.Zips["/repos/ana/library/zipball/1111111aaaa"] = "PK-zip-bytes"u8.ToArray();
        using var destination = new MemoryStream();

        await Source().DownloadAsync(new RepositorySnapshot
        {
            Owner = "ana",
            Repository = "library",
            CommitSha = "1111111aaaa",
            Ref = "main",
        }, destination,
            TestContext.Current.CancellationToken);

        Assert.Equal("PK-zip-bytes", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Fact]
    public async Task Download_OfAnOversizedRepository_IsRefused()
    {
        _github.Zips["/repos/ana/huge/zipball/3333333"] = [];
        _github.DeclaredLength = 200L * 1024 * 1024;

        await Assert.ThrowsAsync<SubmissionSourceUnavailableException>(() => Source().DownloadAsync(
            new RepositorySnapshot { Owner = "ana", Repository = "huge", CommitSha = "3333333", Ref = "main" }, new MemoryStream(), TestContext.Current.CancellationToken));
    }

    private sealed class FakeGitHub : HttpMessageHandler
    {
        public Dictionary<string, string> Json { get; } = [];

        public Dictionary<string, byte[]> Zips { get; } = [];

        public bool RateLimited { get; set; }

        public long? DeclaredLength { get; set; }

        public string? Authorization { get; private set; }

        public string UserAgent { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            UserAgent = request.Headers.UserAgent.ToString();
            var path = request.RequestUri!.AbsolutePath;

            if (RateLimited)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.Forbidden);
                limited.Headers.Add("x-ratelimit-remaining", "0");
                limited.Headers.Add("x-ratelimit-reset", "1800000000");
                return Task.FromResult(limited);
            }

            if (Json.TryGetValue(path, out var json))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }

            if (Zips.TryGetValue(path, out var zip))
            {
                var content = new ByteArrayContent(zip);
                content.Headers.ContentLength = DeclaredLength ?? zip.Length;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
