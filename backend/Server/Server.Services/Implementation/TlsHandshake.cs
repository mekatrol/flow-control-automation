using System.Net.Security;
using System.Security.Authentication;

namespace Server.Services.Implementation;

internal sealed class TlsHandshake : ITlsHandshake
{
    public async Task<Stream> AuthenticateAsync(
        Stream stream,
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var tls = new SslStream(stream, leaveInnerStreamOpen: false);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeoutSource.CancelAfter(timeout);
            await tls.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                },
                timeoutSource.Token);
            return tls;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await tls.DisposeAsync();
            throw new TimeoutException("TLS handshake timed out.");
        }
        catch
        {
            await tls.DisposeAsync();
            throw;
        }
    }
}