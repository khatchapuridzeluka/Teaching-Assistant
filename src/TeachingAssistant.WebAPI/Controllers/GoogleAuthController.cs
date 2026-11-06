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
    /// Opens Google sign-in in the browser on the machine running the API and waits up to 5 minutes for approval. Local development only.
    /// </summary>
    /// <response code="200">Connected (or was already connected)</response>
    /// <response code="400">Not connected: client not configured, access denied, or no approval in time</response>
    [HttpPost("connect")]
    public async Task<ActionResult<GoogleConnectionStatus>> ConnectAsync(CancellationToken cancellationToken)
    {
        var result = await _googleConnectionService.ConnectAsync(cancellationToken);
        return result.State == GoogleConnectionState.Connected ? Ok(result) : BadRequest(result);
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
