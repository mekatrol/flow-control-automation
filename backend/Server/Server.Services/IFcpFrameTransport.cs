namespace Server.Services;

public interface IFcpFrameTransport
{
    Task<ReadOnlyMemory<byte>> TransceiveAsync(
        ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken);
}