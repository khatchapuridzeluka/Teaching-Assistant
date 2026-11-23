using System.Collections.Concurrent;
using System.Security.Cryptography;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Logging;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Infrastructure.Google;

public class GoogleConnectionService : IGoogleConnectionService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly GoogleAuthorization _authorization;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GoogleConnectionService> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pendingStates = new();

    public GoogleConnectionService(
        GoogleAuthorization authorization, TimeProvider timeProvider, ILogger<GoogleConnectionService> logger)
    {
        _authorization = authorization;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_authorization.TryGetFlow(out var flow))
        {
            return NotConfigured();
        }

        var token = await flow.LoadTokenAsync(GoogleAuthorization.UserKey, cancellationToken);
        if (token is null)
        {
            return new GoogleConnectionStatus
            {
                State = GoogleConnectionState.NotConnected,
                GrantedScopes = [],
                Message = "Not connected. Click Connect on the Settings page.",
            };
        }

        var granted = GrantedScopes(token);
        var missing = GoogleAuthorization.Scopes.Except(granted).ToList();
        if (missing.Count > 0)
        {
            return new GoogleConnectionStatus
            {
                State = GoogleConnectionState.ReconnectRequired,
                GrantedScopes = granted,
                Message = $"Missing scopes: {string.Join(", ", missing)}. Connect again and allow all requested access.",
            };
        }

        try
        {
            var credential = new UserCredential(flow, GoogleAuthorization.UserKey, token);
            await credential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
            return new GoogleConnectionStatus { State = GoogleConnectionState.Connected, GrantedScopes = granted };
        }
        catch (TokenResponseException ex)
        {
            _logger.LogWarning("Stored Google token is no longer valid: {GoogleError}", ex.Error.Error);
            return new GoogleConnectionStatus
            {
                State = GoogleConnectionState.ReconnectRequired,
                GrantedScopes = granted,
                Message = $"Stored authorization is no longer valid ({ex.Error.Error}). Connect again.",
            };
        }
    }

    public async Task<GoogleConnectResponse> StartConnectAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status.State is GoogleConnectionState.NotConfigured or GoogleConnectionState.Connected)
        {
            return new GoogleConnectResponse { State = status.State, Message = status.Message };
        }

        if (!_authorization.TryGetFlow(out var flow) || _authorization.RedirectUri is not { } redirectUri)
        {
            return new GoogleConnectResponse
            {
                State = GoogleConnectionState.NotConfigured,
                Message = "Google:RedirectUri is not set. Set it to this app's /api/auth/google/callback address and register the same address on the OAuth client.",
            };
        }

        var now = _timeProvider.GetUtcNow();
        foreach (var (pending, expires) in _pendingStates)
        {
            if (expires <= now)
            {
                _pendingStates.TryRemove(pending, out _);
            }
        }

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _pendingStates[state] = now + StateLifetime;

        var request = (GoogleAuthorizationCodeRequestUrl)flow.CreateAuthorizationCodeRequest(redirectUri);
        request.State = state;
        request.AccessType = "offline";
        request.Prompt = "consent";

        return new GoogleConnectResponse { State = status.State, AuthorizationUrl = request.Build().AbsoluteUri };
    }

    public async Task<GoogleConnectionStatus> CompleteConnectAsync(
        string? code, string? state, string? error, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(state) || !_pendingStates.TryRemove(state, out var expires) || expires <= _timeProvider.GetUtcNow())
        {
            return Failed("This sign-in attempt is unknown or has expired. Click Connect again.");
        }

        if (!string.IsNullOrEmpty(error))
        {
            return Failed($"Google authorization failed ({error}).");
        }

        if (string.IsNullOrEmpty(code))
        {
            return Failed("Google returned no authorization code. Click Connect again.");
        }

        if (!_authorization.TryGetFlow(out var flow) || _authorization.RedirectUri is not { } redirectUri)
        {
            return NotConfigured();
        }

        try
        {
            await flow.ExchangeCodeForTokenAsync(GoogleAuthorization.UserKey, code, redirectUri, cancellationToken);
        }
        catch (TokenResponseException ex)
        {
            _logger.LogWarning("Google authorization failed: {GoogleError}", ex.Error.Error);
            return Failed($"Google authorization failed ({ex.Error.Error}).");
        }

        _authorization.ResetCredential();
        _logger.LogInformation("Google account connected");
        return await GetStatusAsync(cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_authorization.TryGetFlow(out var flow))
        {
            return;
        }

        var token = await flow.LoadTokenAsync(GoogleAuthorization.UserKey, cancellationToken);
        if (token is null)
        {
            return;
        }

        try
        {
            await flow.RevokeTokenAsync(GoogleAuthorization.UserKey, token.RefreshToken ?? token.AccessToken, cancellationToken);
        }
        catch (TokenResponseException ex)
        {
            _logger.LogInformation("Google token was already invalid when revoking: {GoogleError}", ex.Error.Error);
        }

        await flow.DeleteTokenAsync(GoogleAuthorization.UserKey, cancellationToken);
        _authorization.ResetCredential();
        _logger.LogInformation("Google account disconnected");
    }

    private static readonly Dictionary<string, string> ScopeAliases = new()
    {
        ["https://www.googleapis.com/auth/classroom.student-submissions.students.readonly"] =
            "https://www.googleapis.com/auth/classroom.coursework.students.readonly",
    };

    private static List<string> GrantedScopes(TokenResponse token) =>
        (token.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(scope => ScopeAliases.GetValueOrDefault(scope, scope))
            .ToList();

    private static GoogleConnectionStatus Failed(string message) => new()
    {
        State = GoogleConnectionState.NotConnected,
        GrantedScopes = [],
        Message = message,
    };

    private static GoogleConnectionStatus NotConfigured() => new()
    {
        State = GoogleConnectionState.NotConfigured,
        GrantedScopes = [],
        Message = "Google:ClientId and Google:ClientSecret are not set. Add them with dotnet user-secrets.",
    };
}
