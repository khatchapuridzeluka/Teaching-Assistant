using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGoogleConnectionService
{
    Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<GoogleConnectResponse> StartConnectAsync(CancellationToken cancellationToken = default);

    Task<GoogleConnectionStatus> CompleteConnectAsync(
        string? code, string? state, string? error, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
