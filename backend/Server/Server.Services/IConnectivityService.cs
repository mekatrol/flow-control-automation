using Server.Services.Contracts;

namespace Server.Services;

public interface IConnectivityService
{
    Task<ConnectivityResult> TestAsync(
        PointSource source,
        string clientKey,
        CancellationToken cancellationToken);
}