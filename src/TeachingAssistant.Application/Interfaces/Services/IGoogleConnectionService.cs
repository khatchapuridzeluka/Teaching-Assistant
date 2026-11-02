using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Connects the teacher's Google account (read-only Classroom and Drive access).
/// Implemented in Infrastructure, since all of it is Google SDK work.
/// </summary>
public interface IGoogleConnectionService
{
    /// <summary>
    /// Returns the current connection state. Refreshes the stored token if needed to prove it still works.
    /// </summary>
    Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens Google sign-in in the browser on this machine and waits for the teacher to approve.
    /// Returns immediately if a working authorization is already stored.
    /// </summary>
    Task<GoogleConnectionStatus> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the stored authorization at Google and deletes it locally.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
