namespace Server.Services;

/// <summary>Authenticates a validated TCP stream as a TLS client connection.</summary>
public interface ITlsHandshake
{
    /// <summary>Performs certificate and host-name validation within a bounded time budget.</summary>
    /// <param name="stream">An open readable and writable transport stream owned by the caller; it must not already be wrapped in TLS.</param>
    /// <param name="host">The non-empty DNS identity used for SNI and certificate-name validation.</param>
    /// <param name="timeout">A positive finite handshake budget no greater than the enclosing connectivity-test budget.</param>
    /// <param name="cancellationToken">Cancels authentication before the timeout expires.</param>
    /// <returns>An authenticated stream for the same connection; disposing it closes or releases the wrapped transport according to the implementation contract.</returns>
    Task<Stream> AuthenticateAsync(
        Stream stream,
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}