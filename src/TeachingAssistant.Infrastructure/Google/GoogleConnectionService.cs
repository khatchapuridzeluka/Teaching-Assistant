using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Logging;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// Local-development OAuth ("installed app" flow): the browser opens on the machine running the API,
/// and the token is stored in a file. Replaced by a web redirect flow when deployed.
/// </summary>
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
            return new GoogleConnectionStatus(GoogleConnectionState.NotConnected, [], "Not connected. Call POST /auth/google/connect.");
        }

        var granted = GrantedScopes(token);
        var missing = GoogleAuthorization.Scopes.Except(granted).ToList();
        if (missing.Count > 0)
        {
            return new GoogleConnectionStatus(GoogleConnectionState.ReconnectRequired, granted,
                $"Missing scopes: {string.Join(", ", missing)}. Connect again and allow all requested access.");
        }

        // A stored token can be dead (refresh tokens expire after ~7 days in Testing mode), so prove it works.
        try
        {
            var credential = new UserCredential(flow, GoogleAuthorization.UserKey, token);
            await credential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
            return new GoogleConnectionStatus(GoogleConnectionState.Connected, granted, null);
        }
        catch (TokenResponseException ex)
        {
            _logger.LogWarning("Stored Google token is no longer valid: {GoogleError}", ex.Error.Error);
            return new GoogleConnectionStatus(GoogleConnectionState.ReconnectRequired, granted,
                $"Stored authorization is no longer valid ({ex.Error.Error}). Connect again.");
        }
    }

    public async Task<GoogleConnectionStatus> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status.State is GoogleConnectionState.NotConfigured or GoogleConnectionState.Connected)
        {
            return status;
        }

        // The broker reuses any stored token without checking it, so drop a dead one first.
        await _authorization.TokenStore.DeleteAsync<TokenResponse>(GoogleAuthorization.UserKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);

        try
        {
            await GoogleWebAuthorizationBroker.AuthorizeAsync(
                _authorization.ClientSecrets, GoogleAuthorization.Scopes, GoogleAuthorization.UserKey,
                timeout.Token, _authorization.TokenStore);

            _authorization.ResetCredential(); // API calls switch to the new token
            _logger.LogInformation("Google account connected");
            return await GetStatusAsync(cancellationToken);
        }
        catch (TokenResponseException ex)
        {
            _logger.LogWarning("Google authorization failed: {GoogleError}", ex.Error.Error);
            return new GoogleConnectionStatus(GoogleConnectionState.NotConnected, [],
                $"Google authorization failed ({ex.Error.Error}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GoogleConnectionStatus(GoogleConnectionState.NotConnected, [],
                $"No approval in the browser within {ConnectTimeout.TotalMinutes} minutes.");
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
            // Already invalid at Google; still forget it locally.
            _logger.LogInformation("Google token was already invalid when revoking: {GoogleError}", ex.Error.Error);
        }

        await flow.DeleteTokenAsync(GoogleAuthorization.UserKey, cancellationToken);
        _authorization.ResetCredential(); // API calls stop using the revoked token
        _logger.LogInformation("Google account disconnected");
    }

    // Google reports some granted scopes under an older alias of the name we requested.
    private static readonly Dictionary<string, string> ScopeAliases = new()
    {
        ["https://www.googleapis.com/auth/classroom.student-submissions.students.readonly"] =
            "https://www.googleapis.com/auth/classroom.coursework.students.readonly",
    };

    // The granted scopes under the names we request, so they can be compared with GoogleAuthorization.Scopes.
    private static List<string> GrantedScopes(TokenResponse token) =>
        (token.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(scope => ScopeAliases.GetValueOrDefault(scope, scope))
            .ToList();

    private static GoogleConnectionStatus NotConfigured() => new(GoogleConnectionState.NotConfigured, [],
        "Google:ClientId and Google:ClientSecret are not set. Add them with dotnet user-secrets.");
}
