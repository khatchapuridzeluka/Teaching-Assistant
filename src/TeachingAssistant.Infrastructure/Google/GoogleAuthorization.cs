using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.Infrastructure.Google;

public class GoogleAuthorization
{
    public static ImmutableArray<string> Scopes { get; } =
    [
        "https://www.googleapis.com/auth/classroom.courses.readonly",
        "https://www.googleapis.com/auth/classroom.coursework.students.readonly",
        "https://www.googleapis.com/auth/classroom.rosters.readonly",
        "https://www.googleapis.com/auth/classroom.profile.photos",
        "https://www.googleapis.com/auth/drive.readonly",
    ];

    public const string UserKey = "teacher";

    public const string ApplicationName = "Teaching Assistant";

    private readonly GoogleOptions _options;
    private readonly Lazy<GoogleAuthorizationCodeFlow> _flow;
    private volatile UserCredential? _credential;

    public GoogleAuthorization(IOptions<GoogleOptions> options)
    {
        _options = options.Value;
        TokenStore = new FileDataStore(_options.TokenStoreFolder, Path.IsPathRooted(_options.TokenStoreFolder));
        _flow = new Lazy<GoogleAuthorizationCodeFlow>(() => new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = ClientSecrets,
                Scopes = Scopes,
                DataStore = TokenStore,
            }));
    }

    public FileDataStore TokenStore { get; }

    public ClientSecrets ClientSecrets => new() { ClientId = _options.ClientId, ClientSecret = _options.ClientSecret };

    public string? RedirectUri => string.IsNullOrWhiteSpace(_options.RedirectUri) ? null : _options.RedirectUri.Trim();

    public bool TryGetFlow([NotNullWhen(true)] out GoogleAuthorizationCodeFlow? flow)
    {
        flow = IsConfigured ? _flow.Value : null;
        return flow is not null;
    }

    public async Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        if (_credential is { } cached)
        {
            return cached;
        }

        if (!TryGetFlow(out var flow))
        {
            throw new SubmissionSourceNotConnectedException(
                "Google:ClientId and Google:ClientSecret are not set. Add them with dotnet user-secrets.");
        }

        var token = await flow.LoadTokenAsync(UserKey, cancellationToken)
            ?? throw new SubmissionSourceNotConnectedException(
                "Google account is not connected. Call POST /auth/google/connect.");

        return _credential = new UserCredential(flow, UserKey, token);
    }

    public void ResetCredential() => _credential = null;

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret);
}
