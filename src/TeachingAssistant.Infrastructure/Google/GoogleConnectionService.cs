using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Logging;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Infrastructure.Google;

public class GoogleConnectionService : IGoogleConnectionService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMinutes(5);

    private readonly GoogleAuthorization _authorization;
    private readonly ILogger<GoogleConnectionService> _logger;

    public GoogleConnectionService(GoogleAuthorization authorization, ILogger<GoogleConnectionService> logger)
    {
        _authorization = authorization;
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
                Message = "Not connected. Call POST /auth/google/connect.",
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

    public async Task<GoogleConnectionStatus> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status.State is GoogleConnectionState.NotConfigured or GoogleConnectionState.Connected)
        {
            return status;
        }

        await _authorization.TokenStore.DeleteAsync<TokenResponse>(GoogleAuthorization.UserKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);

        try
        {
            await GoogleWebAuthorizationBroker.AuthorizeAsync(
                _authorization.ClientSecrets, GoogleAuthorization.Scopes, GoogleAuthorization.UserKey,
                timeout.Token, _authorization.TokenStore);

            _authorization.ResetCredential();
            _logger.LogInformation("Google account connected");
            return await GetStatusAsync(cancellationToken);
        }
        catch (TokenResponseException ex)
        {
            _logger.LogWarning("Google authorization failed: {GoogleError}", ex.Error.Error);
            return new GoogleConnectionStatus
            {
                State = GoogleConnectionState.NotConnected,
                GrantedScopes = [],
                Message = $"Google authorization failed ({ex.Error.Error}).",
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GoogleConnectionStatus
            {
                State = GoogleConnectionState.NotConnected,
                GrantedScopes = [],
                Message = $"No approval in the browser within {ConnectTimeout.TotalMinutes} minutes.",
            };
        }
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

    private static GoogleConnectionStatus NotConfigured() => new()
    {
        State = GoogleConnectionState.NotConfigured,
        GrantedScopes = [],
        Message = "Google:ClientId and Google:ClientSecret are not set. Add them with dotnet user-secrets.",
    };
}
