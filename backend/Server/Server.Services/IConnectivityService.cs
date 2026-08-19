namespace Server.Services;

/// <summary>Runs bounded, read-only protocol connectivity checks for unsaved or stored point sources.</summary>
public interface IConnectivityService
{
    /// <summary>Validates a source and tests its DNS, transport, security, authentication, and lightweight protocol stages without persisting results.</summary>
    /// <param name="source">The complete source definition to test; credentials must be references and all protocol timeouts must satisfy source limits.</param>
    /// <param name="clientKey">A non-empty stable rate-limit key for the requesting client; it must not contain credentials or other secrets.</param>
    /// <param name="cancellationToken">Cancels the test and all in-flight network operations without mutating the remote system.</param>
    /// <returns>The ordered stage outcomes, bounded latency data, and final success state; protocol failures are represented in the result rather than as successful connectivity.</returns>
    Task<ConnectivityResult> TestAsync(
        PointSource source,
        string clientKey,
        CancellationToken cancellationToken);
}