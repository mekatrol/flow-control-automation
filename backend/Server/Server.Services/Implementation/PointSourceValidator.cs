using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

internal sealed partial class PointSourceValidator : IPointSourceValidator
{
    public void Validate(PointSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Identifier().IsMatch(source.Id))
        {
            throw new PointSourceValidationException(
                "id must be a lowercase hyphenated identifier");
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            throw new PointSourceValidationException("name must be non-empty");
        }

        if (source.CredentialRef is { Length: > 0 } credentialRef
            && !credentialRef.StartsWith("env:", StringComparison.Ordinal)
            && !credentialRef.StartsWith("secret://", StringComparison.Ordinal))
        {
            throw new PointSourceValidationException(
                "credentialRef must use env: or secret://");
        }

        if (source.Timeouts.ConnectMilliseconds is < 100 or > 30000)
        {
            throw new PointSourceValidationException(
                "timeouts.connectMilliseconds must be between 100 and 30000");
        }

        if (source.Timeouts.RequestMilliseconds is { } requestMilliseconds
            && requestMilliseconds is < 100 or > 60000)
        {
            throw new PointSourceValidationException(
                "timeouts.requestMilliseconds must be between 100 and 60000 when set");
        }

        var address = source.Kind switch
        {
            "homeAssistant" => RequireBaseUrl(source),
            "httpJson" => ValidateHttpJson(source),
            "mqtt" => ValidateMqtt(source),
            _ => throw new PointSourceValidationException(
                "kind must be homeAssistant, mqtt, or httpJson"),
        };
        ValidateAddress(source, address);
    }

    private static string RequireBaseUrl(PointSource source) =>
        string.IsNullOrEmpty(source.Connection.BaseUrl)
            ? throw new PointSourceValidationException("connection.baseUrl is required")
            : source.Connection.BaseUrl;

    private static string ValidateHttpJson(PointSource source)
    {
        var address = RequireBaseUrl(source);
        if (source.Connection.AllowedReadMethods?.Any(
            method => method is not ("GET" or "HEAD")) == true)
        {
            throw new PointSourceValidationException(
                "only GET and HEAD are allowed read methods");
        }

        if (source.Connection.MaximumResponseBytes is not (>= 1 and <= 10 << 20))
        {
            throw new PointSourceValidationException(
                "maximumResponseBytes must be between 1 and 10485760");
        }

        return address;
    }

    private static string ValidateMqtt(PointSource source)
    {
        if (string.IsNullOrEmpty(source.Connection.BrokerUrl))
        {
            throw new PointSourceValidationException("connection.brokerUrl is required");
        }

        if (source.Connection.Qos is not (>= 0 and <= 2))
        {
            throw new PointSourceValidationException("connection.qos must be 0, 1, or 2");
        }

        var topic = source.Connection.TestTopic ?? string.Empty;
        if (topic.IndexOfAny(['+', '#', '\0']) >= 0 || topic.Length > ushort.MaxValue)
        {
            throw new PointSourceValidationException(
                "connection.testTopic must be an exact MQTT topic without wildcards");
        }

        return source.Connection.BrokerUrl;
    }

    private static void ValidateAddress(PointSource source, string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new PointSourceValidationException(
                "connection URL must be absolute and must not contain credentials");
        }

        var allowedScheme = source.Kind == "mqtt"
            ? uri.Scheme is "mqtt" or "mqtts"
            : uri.Scheme == Uri.UriSchemeHttps;
        if (!allowedScheme)
        {
            throw new PointSourceValidationException("connection URL scheme is not allowed");
        }

        if ((uri.Scheme is "https" or "mqtts") && !source.Tls.VerifyServerCertificate)
        {
            throw new PointSourceValidationException(
                "TLS server certificate verification must be enabled");
        }
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
}