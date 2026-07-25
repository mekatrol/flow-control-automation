using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Server.Services.Implementation;

internal sealed class HttpProtocolCheck(IDnsLookup dns) : IHttpProtocolCheck
{
    private const int DefaultMaximumResponseBytes = 64 << 10;
    private const int MaximumRedirects = 3;

    public async Task<string?> CheckAsync(
        PointSource source,
        string credential,
        IReadOnlyList<IPAddress> pinnedAddresses,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(source.Connection.BaseUrl!);
        if (source.Kind == "home_assistant")
        {
            endpoint = new Uri(
                endpoint,
                endpoint.AbsolutePath.TrimEnd('/') + "/api/");
        }

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
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
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
                return "connection test cancelled";
            }
            catch
            {
                return "HTTP protocol check failed";
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode)
                    && source.Connection.FollowRedirects == true
                    && response.Headers.Location is not null)
                {
                    if (redirects >= MaximumRedirects)
                    {
                        return "too many redirects";
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
                        return "connection test cancelled";
                    }
                    catch
                    {
                        return "redirect host lookup failed";
                    }

                    if (addresses.Count == 0)
                    {
                        return "redirect host lookup failed";
                    }

                    if (addresses.Any(address => ConnectivityPolicy.IsForbidden(
                        address,
                        source.Connection.AllowPrivateNetwork == true)))
                    {
                        return "redirect destination is forbidden";
                    }

                    redirects++;
                    continue;
                }

                var maximumBytes = source.Connection.MaximumResponseBytes
                    ?? DefaultMaximumResponseBytes;
                try
                {
                    await using var body =
                        await response.Content.ReadAsStreamAsync(cancellationToken);
                    var buffer = new byte[Math.Min(maximumBytes + 1, 81920)];
                    long total = 0;
                    while (true)
                    {
                        var read = await body.ReadAsync(buffer, cancellationToken);
                        if (read == 0)
                        {
                            break;
                        }

                        total += read;
                        if (total > maximumBytes)
                        {
                            return "HTTP response exceeded the configured size limit";
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return "connection test cancelled";
                }
                catch
                {
                    return "HTTP response could not be read";
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden)
                {
                    return "authentication was rejected";
                }

                if ((int)response.StatusCode >= 400)
                {
                    return $"HTTP protocol check returned status {(int)response.StatusCode}";
                }

                return null;
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