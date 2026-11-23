using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Infrastructure.Google;

namespace TeachingAssistant.Tests.Infrastructure;

// No test reaches Google: with no stored token the status is read from the empty token folder,
// and every failing callback is rejected before a code would be exchanged.
public sealed class GoogleConnectionServiceTests : IDisposable
{
    private const string RedirectUri = "http://localhost:5173/api/auth/google/callback";

    private readonly string _tokenFolder = Path.Combine(Path.GetTempPath(), "ta-google-" + Guid.NewGuid().ToString("N"));
    private readonly SettableClock _clock = new(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_tokenFolder))
        {
            Directory.Delete(_tokenFolder, recursive: true);
        }
    }

    [Fact]
    public async Task StartConnect_ReturnsGoogleConsentUrl_WithTheCallbackStateOfflineAccessAndReadOnlyScopes()
    {
        var service = CreateService();

        var result = await service.StartConnectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GoogleConnectionState.NotConnected, result.State);
        Assert.NotNull(result.AuthorizationUrl);
        var url = new Uri(result.AuthorizationUrl);
        Assert.Equal("accounts.google.com", url.Host);
        var query = HttpUtility.ParseQueryString(url.Query);
        Assert.Equal("client-id", query["client_id"]);
        Assert.Equal(RedirectUri, query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.Equal(GoogleAuthorization.Scopes.Order(), query["scope"]!.Split(' ').Order());
    }

    [Fact]
    public async Task StartConnect_WithoutRedirectUri_IsNotConfiguredAndGivesNoUrl()
    {
        var service = CreateService(redirectUri: null);

        var result = await service.StartConnectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GoogleConnectionState.NotConfigured, result.State);
        Assert.Null(result.AuthorizationUrl);
        Assert.Contains("Google:RedirectUri", result.Message);
    }

    [Fact]
    public async Task StartConnect_WithoutClientSecrets_IsNotConfiguredAndGivesNoUrl()
    {
        var service = CreateService(clientId: null);

        var result = await service.StartConnectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GoogleConnectionState.NotConfigured, result.State);
        Assert.Null(result.AuthorizationUrl);
    }

    [Fact]
    public async Task CompleteConnect_WithAStateThisServiceNeverIssued_IsRejected()
    {
        var service = CreateService();

        var result = await service.CompleteConnectAsync("code", "forged", null, TestContext.Current.CancellationToken);

        Assert.Equal(GoogleConnectionState.NotConnected, result.State);
        Assert.Contains("unknown or has expired", result.Message);
    }

    [Fact]
    public async Task CompleteConnect_WhenTheTeacherDeniesAccess_ReportsGooglesErrorAndTheStateCannotBeReused()
    {
        var service = CreateService();
        var state = await IssueStateAsync(service);

        var denied = await service.CompleteConnectAsync(null, state, "access_denied", TestContext.Current.CancellationToken);
        var reused = await service.CompleteConnectAsync("code", state, null, TestContext.Current.CancellationToken);

        Assert.Equal(GoogleConnectionState.NotConnected, denied.State);
        Assert.Contains("access_denied", denied.Message);
        Assert.Contains("unknown or has expired", reused.Message);
    }

    [Fact]
    public async Task CompleteConnect_AfterTheStateExpired_IsRejected()
    {
        var service = CreateService();
        var state = await IssueStateAsync(service);
        _clock.Now += TimeSpan.FromMinutes(11);

        var result = await service.CompleteConnectAsync("code", state, null, TestContext.Current.CancellationToken);

        Assert.Contains("unknown or has expired", result.Message);
    }

    [Fact]
    public async Task CompleteConnect_WithoutACode_IsRejected()
    {
        var service = CreateService();
        var state = await IssueStateAsync(service);

        var result = await service.CompleteConnectAsync(null, state, null, TestContext.Current.CancellationToken);

        Assert.Contains("no authorization code", result.Message);
    }

    private static async Task<string> IssueStateAsync(GoogleConnectionService service)
    {
        var started = await service.StartConnectAsync(TestContext.Current.CancellationToken);
        return HttpUtility.ParseQueryString(new Uri(started.AuthorizationUrl!).Query)["state"]!;
    }

    private GoogleConnectionService CreateService(string? clientId = "client-id", string? redirectUri = RedirectUri)
    {
        var options = Options.Create(new GoogleOptions
        {
            ClientId = clientId,
            ClientSecret = "client-secret",
            RedirectUri = redirectUri,
            TokenStoreFolder = _tokenFolder,
        });
        return new GoogleConnectionService(new GoogleAuthorization(options), _clock, NullLogger<GoogleConnectionService>.Instance);
    }

    private sealed class SettableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
