using System.Diagnostics;
using System.Net;

namespace Server.Services.Implementation;

internal sealed class ConnectivityService(
    IPointSourceValidator validator,
    ICredentialResolver credentialResolver,
    IDnsLookup dns,
    ITcpConnectionFactory tcp,
    ITlsHandshake tls,
    IHttpProtocolCheck http,
    IMqttProtocolCheck mqtt,
    IConnectivityClock clock,
    ConnectivityRateLimiter rateLimiter) : IConnectivityService
{
    public async Task<ConnectivityResult> TestAsync(
        PointSource source,
        string clientKey,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var stages = new List<ConnectivityStage>();
        HttpResponsePreview? httpResponse = null;
        ConnectivityResult Result(string status) =>
            new(
                status,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                stages,
                httpResponse);
        ConnectivityResult Failed(string name, string diagnostic)
        {
            stages.Add(new(name, "failed", diagnostic));
            return Result("failed");
        }

        if (!rateLimiter.Allow(clientKey, clock.UtcNow))
        {
            return Failed("policy", "connection test rate limit exceeded");
        }

        try
        {
            validator.Validate(source);
        }
        catch (PointSourceValidationException exception)
        {
            return Failed("validation", exception.Message);
        }

        var target = new Uri(
            source.Kind == "mqtt"
                ? source.Connection.BrokerUrl!
                : source.Connection.BaseUrl!);
        var connectTimeout = TimeSpan.FromMilliseconds(
            source.Timeouts.ConnectMilliseconds);
        IReadOnlyList<IPAddress> addresses;
        using var dnsTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        dnsTimeout.CancelAfter(connectTimeout);
        try
        {
            addresses = await dns.LookupAsync(target.Host, dnsTimeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("dns", "connection test cancelled");
        }
        catch (OperationCanceledException) when (dnsTimeout.IsCancellationRequested)
        {
            return Failed("dns", "host lookup timed out");
        }
        catch (OperationCanceledException)
        {
            return Failed("dns", "connection test cancelled");
        }
        catch
        {
            return Failed("dns", "host lookup failed");
        }

        if (addresses.Count == 0)
        {
            return Failed("dns", "host lookup failed");
        }

        if (addresses.Any(address => ConnectivityPolicy.IsForbidden(
            address,
            source.Connection.AllowPrivateNetwork == true)))
        {
            return Failed(
                "dns",
                "destination address is forbidden by outbound network policy");
        }

        stages.Add(new("dns", "passed"));
        var port = target.IsDefaultPort
            ? target.Scheme switch
            {
                "mqtts" => 8883,
                "mqtt" => 1883,
                "http" => 80,
                _ => 443,
            }
            : target.Port;
        Stream connection;
        try
        {
            connection = await tcp.ConnectAsync(
                addresses[0].ToString(),
                port,
                connectTimeout,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failed("tcp", "connection test cancelled");
        }
        catch
        {
            return Failed("tcp", "TCP connection failed");
        }

        try
        {
            stages.Add(new("tcp", "passed"));
            if (target.Scheme is "https" or "mqtts")
            {
                try
                {
                    connection = await tls.AuthenticateAsync(
                        connection,
                        target.Host,
                        connectTimeout,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return Failed("tls", "connection test cancelled");
                }
                catch
                {
                    return Failed(
                        "tls",
                        "TLS handshake or certificate verification failed");
                }

                stages.Add(new("tls", "passed"));
            }

            string credential;
            try
            {
                credential = await credentialResolver.ResolveAsync(
                    source.CredentialRef ?? string.Empty,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Failed("authentication", "connection test cancelled");
            }
            catch (CredentialResolutionException exception)
            {
                return Failed("authentication", exception.Message);
            }
            catch
            {
                return Failed(
                    "authentication",
                    "referenced credential could not be resolved");
            }

            stages.Add(new("authentication", "passed"));
            string? diagnostic;
            try
            {
                if (source.Kind == "mqtt")
                {
                    diagnostic = await mqtt.CheckAsync(
                        connection,
                        source,
                        credential,
                        cancellationToken);
                }
                else
                {
                    var check = await http.CheckAsync(
                        source,
                        credential,
                        addresses,
                        cancellationToken);
                    diagnostic = check.Diagnostic;
                    httpResponse = check.Response;
                }
            }
            catch (OperationCanceledException)
            {
                return Failed("protocol", "connection test cancelled");
            }
            catch
            {
                diagnostic = source.Kind == "mqtt"
                    ? "MQTT protocol check failed"
                    : "HTTP protocol check failed";
            }

            if (diagnostic is not null)
            {
                return Failed("protocol", diagnostic);
            }

            stages.Add(new("protocol", "passed"));
            return Result("passed");
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}