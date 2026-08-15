namespace Server.Services;

/// <summary>Creates bounded TCP streams for validated connectivity-test destinations.</summary>
public interface ITcpConnectionFactory
{
    /// <summary>Connects to one host and port within the supplied timeout budget.</summary>
    /// <param name="host">A non-empty validated DNS name or IP literal; callers must complete SSRF checks before connecting.</param>
    /// <param name="port">The destination TCP port in the inclusive range 1 through 65535.</param>
    /// <param name="timeout">A positive finite connection budget no greater than the containing connectivity-test budget.</param>
    /// <param name="cancellationToken">Cancels connection establishment earlier than <paramref name="timeout"/>.</param>
    /// <returns>An open readable and writable network stream owned by the caller.</returns>
    Task<Stream> ConnectAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}