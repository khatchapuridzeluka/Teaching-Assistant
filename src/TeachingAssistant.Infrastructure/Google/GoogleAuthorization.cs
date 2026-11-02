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
    public static readonly string[] Scopes =
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

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret);

    public ClientSecrets ClientSecrets => new() { ClientId = _options.ClientId, ClientSecret = _options.ClientSecret };

    /// <summary>One flow for the app's lifetime; only valid when <see cref="IsConfigured"/> is true.</summary>
    public GoogleAuthorizationCodeFlow Flow => _flow.Value;
}
