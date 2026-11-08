using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// Shared Google OAuth plumbing: client secrets, the read-only scopes and the token store.
/// Used by <see cref="GoogleConnectionService"/> (connect/status) and the Classroom and Drive adapters (API calls).
/// </summary>
public class GoogleAuthorization
{
    // Read-only on purpose: the app must never be able to write grades or change anything in Classroom or Drive.
    // ImmutableArray so nothing can change the list at runtime (a plain array's items could be overwritten).
    public static ImmutableArray<string> Scopes { get; } =
    [
        "https://www.googleapis.com/auth/classroom.courses.readonly",
        "https://www.googleapis.com/auth/classroom.coursework.students.readonly",
        "https://www.googleapis.com/auth/classroom.rosters.readonly",
        "https://www.googleapis.com/auth/classroom.profile.photos",
        "https://www.googleapis.com/auth/drive.readonly",
    ];

    // Single-user app: one stored token under a fixed key.
    public const string UserKey = "teacher";

    // Sent in the User-Agent of every Google API request.
    public const string ApplicationName = "Teaching Assistant";

    private readonly GoogleOptions _options;
    private readonly Lazy<GoogleAuthorizationCodeFlow> _flow;
    private volatile UserCredential? _credential;

    public GoogleAuthorization(IOptions<GoogleOptions> options)
    {
        _options = options.Value;
        TokenStore = new FileDataStore(_options.TokenStoreFolder);
        _flow = new Lazy<GoogleAuthorizationCodeFlow>(() => new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = ClientSecrets,
                Scopes = Scopes,
                DataStore = TokenStore,
            }));
    }

    /// <summary>File store under %APPDATA% holding the teacher's tokens (plain JSON; local development only).</summary>
    public FileDataStore TokenStore { get; }

    public ClientSecrets ClientSecrets => new() { ClientId = _options.ClientId, ClientSecret = _options.ClientSecret };

    /// <summary>
    /// Returns the app's single OAuth flow, or false when Google:ClientId/ClientSecret are not configured.
    /// The only way to reach the flow, so an unconfigured flow is never built or used.
    /// </summary>
    public bool TryGetFlow([NotNullWhen(true)] out GoogleAuthorizationCodeFlow? flow)
    {
        flow = IsConfigured ? _flow.Value : null;
        return flow is not null;
    }

    /// <summary>
    /// Returns the credential for calling Google APIs as the teacher. It adds the access token to every request
    /// and refreshes it when stale. Throws <see cref="SubmissionSourceNotConnectedException"/> when the client
    /// is not configured or no token is stored; an expired token only fails on the first real request.
    /// The same credential is shared by all calls (one refresh for everyone, safe in parallel) until
    /// <see cref="ResetCredential"/> is called.
    /// </summary>
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

        // Two first calls at once may both build one; either is fine, and from then on all calls share it.
        return _credential = new UserCredential(flow, UserKey, token);
    }

    /// <summary>
    /// Forgets the shared credential so the next call reads the stored token again. Call after connecting
    /// or disconnecting; otherwise calls would keep using the previous (revoked or replaced) token.
    /// </summary>
    public void ResetCredential() => _credential = null;

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret);
}
