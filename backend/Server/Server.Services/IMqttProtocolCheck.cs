namespace Server.Services;

/// <summary>Performs the bounded MQTT protocol stage of a read-only point-source connectivity test.</summary>
public interface IMqttProtocolCheck
{
    /// <summary>Negotiates one short-lived MQTT client session and disconnects without subscribing, publishing, or retaining state.</summary>
    /// <param name="stream">An open broker transport, already protected with TLS when the source requires it; ownership remains with the caller.</param>
    /// <param name="source">The validated MQTT source whose client, protocol, and authentication settings govern the check.</param>
    /// <param name="credential">The resolved credential value for the source, or an empty string only when anonymous access is configured.</param>
    /// <param name="cancellationToken">Cancels protocol I/O within the overall connectivity-test budget.</param>
    /// <returns>Optional bounded broker identity text safe for diagnostics, or <see langword="null"/> when the broker supplies no identity.</returns>
    Task<string?> CheckAsync(
        Stream stream,
        PointSource source,
        string credential,
        CancellationToken cancellationToken);
}