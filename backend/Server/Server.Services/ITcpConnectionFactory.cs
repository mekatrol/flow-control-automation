namespace Server.Services;

public interface ITcpConnectionFactory
{
    Task<Stream> ConnectAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}