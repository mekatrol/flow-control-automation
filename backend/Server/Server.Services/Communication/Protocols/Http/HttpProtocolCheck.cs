using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace Server.Services.Communication.Protocols.Http;

internal sealed class HttpProtocolCheck(IDnsLookup dns) : IHttpProtocolCheck
{
    private const int DefaultMaximumResponseBytes = 64 << 10;
    private const int MaximumRedirects = 3;

    public async Task<HttpProtocolCheckResult> CheckAsync(
        PointSource source,
        string credential,
        IReadOnlyList<IPAddress> pinnedAddresses,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(source.Connection.BaseUrl!);

        if (source.Kind == "homeAssistant")
        {
            endpoint = new Uri(
                endpoint,
                endpoint.AbsolutePath.TrimEnd('/') + "/api/");
        }

        return await ReadAsync(source, endpoint, credential, pinnedAddresses, cancellationToken);
    }

    public async Task<HttpProtocolCheckResult> ReadAsync(
        PointSource source,
        Uri endpoint,
        string credential,
        IReadOnlyList<IPAddress> pinnedAddresses,
        CancellationToken cancellationToken)
        => await SendAsync(
            source,
            endpoint,
            HttpMethod.Get,
            null,
            "application/json",
            credential,
            pinnedAddresses,
            cancellationToken);

    public async Task<HttpProtocolCheckResult> WriteAsync(
        PointSource source,
        Uri endpoint,
        string method,
        string body,
        string contentType,
        string credential,
        IReadOnlyList<IPAddress> pinnedAddresses,
        CancellationToken cancellationToken) =>
        await SendAsync(
            source,
            endpoint,
            new HttpMethod(method),
            body,
            contentType,
            credential,
            pinnedAddresses,
            cancellationToken);

    private async Task<HttpProtocolCheckResult> SendAsync(
        PointSource source,
        Uri endpoint,
        HttpMethod method,
        string? body,
        string contentType,
        string credential,
        IReadOnlyList<IPAddress> pinnedAddresses,
        CancellationToken cancellationToken)
    {
        var redirects = 0;
        var addresses = pinnedAddresses;

        while (true)
        {
            using var handler = CreateHandler(endpoint, addresses);
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(
                    source.Timeouts.RequestMilliseconds
                    ?? source.Timeouts.ConnectMilliseconds)
            };
            using var request = new HttpRequestMessage(method, endpoint);

            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, contentType);
                request.Content.Headers.ContentType!.CharSet = null;
            }

            if (credential.Length > 0)
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", credential);
            }

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new("connection test cancelled");
            }
            catch
            {
                return new("HTTP protocol check failed");
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode)
                    && source.Connection.FollowRedirects == true
                    && response.Headers.Location is not null)
                {
                    if (redirects >= MaximumRedirects)
                    {
                        return new("too many redirects");
                    }

                    endpoint = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(endpoint, response.Headers.Location);

                    try
                    {
                        addresses = await dns.LookupAsync(endpoint.Host, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return new("connection test cancelled");
                    }
                    catch
                    {
                        return new("redirect host lookup failed");
                    }

                    if (addresses.Count == 0)
                    {
                        return new("redirect host lookup failed");
                    }

                    if (addresses.Any(address => Server.Services.Communication.Network.ConnectivityPolicy.IsForbidden(
                        address,
                        source.Connection.AllowPrivateNetwork == true)))
                    {
                        return new("redirect destination is forbidden");
                    }

                    redirects++;
                    continue;
                }

                var maximumBytes = source.Connection.MaximumResponseBytes
                    ?? DefaultMaximumResponseBytes;
                await using var preview = new MemoryStream();

                try
                {
                    await using var responseBody =
                        await response.Content.ReadAsStreamAsync(cancellationToken);
                    var buffer = new byte[Math.Min(maximumBytes + 1, 81920)];
                    long total = 0;

                    while (true)
                    {
                        var read = await responseBody.ReadAsync(buffer, cancellationToken);

                        if (read == 0)
                        {
                            break;
                        }

                        total += read;

                        if (total > maximumBytes)
                        {
                            return new("HTTP response exceeded the configured size limit");
                        }

                        await preview.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new("connection test cancelled");
                }
                catch
                {
                    return new("HTTP response could not be read");
                }

                var responsePreview = new HttpResponsePreview(
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    response.Content.Headers.ContentType?.ToString(),
                    Encoding.UTF8.GetString(preview.ToArray()));

                if (response.StatusCode is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden)
                {
                    return new("authentication was rejected", responsePreview);
                }

                if ((int)response.StatusCode >= 400)
                {
                    return new(
                        $"HTTP protocol check returned status {(int)response.StatusCode}",
                        responsePreview);
                }

                return new(null, responsePreview);
            }
        }
    }

    private static SocketsHttpHandler CreateHandler(
        Uri endpoint,
        IReadOnlyList<IPAddress> addresses)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            SslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = endpoint.Host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            },
            ConnectCallback = async (context, cancellationToken) =>
                {
                    var port = context.DnsEndPoint.Port;
                    Exception? lastException = null;

                    foreach (var address in addresses)
                    {
                        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                        try
                        {
                            await socket.ConnectAsync(
                                new IPEndPoint(address, port),
                                cancellationToken);

                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch (Exception exception)
                        {
                            socket.Dispose();
                            lastException = exception;
                        }
                    }

                    throw lastException ?? new SocketException((int)SocketError.HostNotFound);
                }
        };

        return handler;
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
}
