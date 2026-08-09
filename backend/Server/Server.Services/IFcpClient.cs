namespace Server.Services;

public interface IFcpClient
{
    Task<ReadOnlyMemory<byte>> ExchangeAuthenticatedAsync(
        byte operation,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken);
}
