using Microsoft.AspNetCore.Mvc;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.WebAPI.Controllers;

[ApiController]
[Route("auth/google")]
[Produces("application/json")]
public class GoogleAuthController : ControllerBase
{
    private readonly IGoogleConnectionService _googleConnectionService;

    public GoogleAuthController(IGoogleConnectionService googleConnectionService)
    {
        _googleConnectionService = googleConnectionService;
    }

    /// <summary>
    /// Returns whether the teacher's Google account is connected with the required read-only scopes.
    /// </summary>
    /// <response code="200">Current connection state (NotConfigured, NotConnected, Connected or ReconnectRequired)</response>
    [HttpGet("status")]
    public async Task<ActionResult<GoogleConnectionStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        return Ok(await _googleConnectionService.GetStatusAsync(cancellationToken));
    }

    /// <summary>
    /// Returns Google's consent page for the browser to open; after approval Google redirects to the callback.
    /// </summary>
    /// <response code="200">Consent page URL to open, or no URL when already connected</response>
    /// <response code="400">Google access is not configured (client ID, client secret or redirect URI missing)</response>
    [HttpPost("connect")]
    public async Task<ActionResult<GoogleConnectResponse>> ConnectAsync(CancellationToken cancellationToken)
    {
        var result = await _googleConnectionService.StartConnectAsync(cancellationToken);
        return result.State == GoogleConnectionState.NotConfigured ? BadRequest(result) : Ok(result);
    }

    /// <summary>
    /// Receives Google's redirect after the consent page, stores the authorization and sends the browser back to Settings.
    /// </summary>
    /// <response code="302">Back to the Settings page, with the outcome in the query string</response>
    [HttpGet("callback")]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var result = await _googleConnectionService.CompleteConnectAsync(code, state, error, cancellationToken);
        if (result.State == GoogleConnectionState.Connected)
        {
            return Redirect("/settings?google=connected");
        }

        return Redirect($"/settings?google=failed&message={Uri.EscapeDataString(result.Message ?? "Google authorization failed.")}");
    }

    /// <summary>
    /// Revokes the app's access at Google and deletes the stored token.
    /// </summary>
    /// <response code="204">Disconnected (or was not connected)</response>
    [HttpPost("disconnect")]
    public async Task<IActionResult> DisconnectAsync(CancellationToken cancellationToken)
    {
        await _googleConnectionService.DisconnectAsync(cancellationToken);
        return NoContent();
    }
}
