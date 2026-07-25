using System.Net.Sockets;

namespace Server.Services.Implementation;

internal sealed class TcpConnectionFactory : ITcpConnectionFactory
{
    public async Task<Stream> ConnectAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeoutSource.CancelAfter(timeout);
            await socket.ConnectAsync(host, port, timeoutSource.Token);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new TimeoutException("TCP connection timed out.");
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}