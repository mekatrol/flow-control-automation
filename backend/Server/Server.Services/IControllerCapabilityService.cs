namespace Server.Services;

/// <summary>Negotiates versioned protocol capabilities with a discovered controller.</summary>
public interface IControllerCapabilityService
{
    /// <summary>Reads and validates the controller capability response without changing controller configuration or runtime state.</summary>
    /// <param name="controller">A discovered descriptor with a non-empty connection identity supported by the configured transport.</param>
    /// <param name="cancellationToken">Cancels connection or protocol I/O without caching a partial capability response.</param>
    /// <returns>The validated protocol version, feature flags, and positive transfer limits advertised by the controller.</returns>
    Task<ControllerProtocolCapabilities> GetAsync(
        ControllerConnectionDescriptor controller,
        CancellationToken cancellationToken);
}