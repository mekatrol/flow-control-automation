using Server.Services.Contracts;

namespace Server.Services;

public interface IControllerDiscoveryService
{
    Task<IReadOnlyList<ControllerConnectionDescriptor>> ListAsync(
        CancellationToken cancellationToken);
}
