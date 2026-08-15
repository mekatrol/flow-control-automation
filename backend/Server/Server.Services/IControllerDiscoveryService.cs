using Server.Services.Contracts;

namespace Server.Services;

/// <summary>Discovers locally available controller connection endpoints without opening a debug session.</summary>
public interface IControllerDiscoveryService
{
    /// <summary>Lists controller endpoints that are currently eligible for connection.</summary>
    /// <param name="cancellationToken">Cancels device enumeration without changing controller state.</param>
    /// <returns>A deterministic list of distinct connection descriptors; the list is empty when no controller is available.</returns>
    Task<IReadOnlyList<ControllerConnectionDescriptor>> ListAsync(
        CancellationToken cancellationToken);
}