namespace Server.Services;

/// <summary>Provides authenticated request-response operations over the Flow Controller Protocol.</summary>
public interface IFcpClient
{
    /// <summary>Authenticates, sends, and verifies one application-level protocol exchange.</summary>
    /// <param name="operation">The protocol operation code in the inclusive byte range 0 through 255; only codes supported by the negotiated controller capabilities are valid.</param>
    /// <param name="payload">The operation payload, which may be empty but must not exceed the negotiated frame limit.</param>
    /// <param name="cancellationToken">Cancels authentication or transport I/O without treating cancellation as a controller response.</param>
    /// <returns>The verified response payload with framing and authentication metadata removed.</returns>
    Task<ReadOnlyMemory<byte>> ExchangeAuthenticatedAsync(
        byte operation,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken);
}