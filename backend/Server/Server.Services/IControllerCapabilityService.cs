using Server.Services.Contracts;

namespace Server.Services;

public interface IControllerCapabilityService
{
    Task<ControllerProtocolCapabilities> GetAsync(
        ControllerConnectionDescriptor controller,
        CancellationToken cancellationToken);
}