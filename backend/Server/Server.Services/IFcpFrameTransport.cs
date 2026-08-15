namespace Server.Services;

/// <summary>Exchanges one framed Flow Controller Protocol request with a controller transport.</summary>
public interface IFcpFrameTransport
{
    /// <summary>Sends one complete encoded request and receives its complete bounded response frame.</summary>
    /// <param name="request">The non-empty encoded FCP frame; its size and integrity fields must satisfy the protocol limits.</param>
    /// <param name="cancellationToken">Cancels pending I/O; cancellation must leave the transport reusable or cause it to be discarded.</param>
    /// <returns>The complete response frame as immutable bytes; ownership of the returned memory remains with the result.</returns>
    Task<ReadOnlyMemory<byte>> TransceiveAsync(
        ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken);
}