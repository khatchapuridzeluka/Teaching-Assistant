using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// Shared Google OAuth plumbing: client secrets, the read-only scopes and the token store.
/// Used by <see cref="GoogleConnectionService"/> (connect/status) and the Classroom source (API calls).
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
        "https://www.googleapis.com/auth/drive.readonly",
    ];

    // Single-user app: one stored token under a fixed key.
    public const string UserKey = "teacher";

    private readonly GoogleOptions _options;
    private readonly Lazy<GoogleAuthorizationCodeFlow> _flow;

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

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret);
}
