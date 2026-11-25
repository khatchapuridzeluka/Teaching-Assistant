using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Options;
using TeachingAssistant.Infrastructure.Google;

namespace TeachingAssistant.Tests.Infrastructure;

// The first page load after a restart asks for courses, assignments and the connection status at once. Every request used to
// build its own credential and refresh the expired token, and the parallel writes of the token file failed with "being used
// by another process", which came back as a 500. These run without network: nothing here refreshes a token.
public sealed class GoogleAuthorizationTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ta-google-auth-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task ParallelFirstRequests_AllShareOneCredential()
    {
        var authorization = Authorization();
        await authorization.TokenStore.StoreAsync(GoogleAuthorization.UserKey, Token());

        var credentials = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            authorization.GetCredentialAsync(TestContext.Current.CancellationToken))));

        Assert.Single(credentials.Distinct());
    }

    [Fact]
    public async Task ParallelTokenWrites_DoNotCollideOnTheFile()
    {
        var authorization = Authorization();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
            authorization.TokenStore.StoreAsync(GoogleAuthorization.UserKey, Token()))));

        var stored = await authorization.TokenStore.GetAsync<TokenResponse>(GoogleAuthorization.UserKey);
        Assert.Equal("refresh", stored.RefreshToken);
    }

    [Fact]
    public async Task AfterReset_TheNextRequestLoadsTheStoredTokenAgain()
    {
        var authorization = Authorization();
        await authorization.TokenStore.StoreAsync(GoogleAuthorization.UserKey, Token());
        var first = await authorization.GetCredentialAsync(TestContext.Current.CancellationToken);

        authorization.ResetCredential();
        var second = await authorization.GetCredentialAsync(TestContext.Current.CancellationToken);

        Assert.NotSame(first, second);
    }

    private GoogleAuthorization Authorization() =>
        new(Options.Create(new GoogleOptions { ClientId = "id", ClientSecret = "secret", TokenStoreFolder = _folder }));

    private static TokenResponse Token() =>
        new() { AccessToken = "access", RefreshToken = "refresh", ExpiresInSeconds = 3600, IssuedUtc = DateTime.UtcNow };
}
