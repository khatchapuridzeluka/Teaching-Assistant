using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGoogleConnectionService
{
    Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<GoogleConnectionStatus> ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
