using Server.Common.Contracts.Templating;
using System.Text.RegularExpressions;

namespace Server.Services.Points.Validation;

internal sealed partial class PointSourceValidator(ITemplateService? templates = null) : IPointSourceValidator
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
            PointSourceKind.Virtual or PointSourceKind.Physical => null,
            PointSourceKind.HomeAssistant => RequireBaseUrl(source),
            PointSourceKind.HttpJson => ValidateHttpJson(source),
            PointSourceKind.Mqtt => ValidateMqtt(source),
            _ => throw new PointSourceValidationException(
                "kind must be virtual, physical, homeAssistant, mqtt, or httpJson")
        };

        if (address is not null)
        {
            ValidateAddress(source, address);
        }

        ValidateAggregate(source);
    }

    public void Validate(PointSource source, IEnumerable<PointSource> existingSources)
    {
        Validate(source);
        ArgumentNullException.ThrowIfNull(existingSources);
        var ownedIds = source.Points.Select(point => point.Id).ToHashSet(StringComparer.Ordinal);
        var duplicate = existingSources
            .Where(existing => existing.Id != source.Id)
            .SelectMany(existing => existing.Points)
            .FirstOrDefault(point => ownedIds.Contains(point.Id));

        if (duplicate is not null)
        {
            throw new PointSourceValidationException($"point id '{duplicate.Id}' must be globally unique");
        }
    }

    private void ValidateAggregate(PointSource source)
    {
        RejectDuplicateIds(source.Mappings.Select(mapping => mapping.Id), "mapping");
        RejectDuplicateIds(source.Points.Select(point => point.Id), "point");

        foreach (var mapping in source.Mappings)
        {
            if (!Identifier().IsMatch(mapping.Id))
            {
                throw new PointSourceValidationException("mapping id must be a lowercase hyphenated identifier");
            }

            RejectDuplicateAliases(mapping.Aliases);
            ValidateMappingForKind(source.Kind, mapping);
            ValidateTemplate(mapping, "read", mapping.Read?.Template);
            ValidateTemplate(mapping, "command", mapping.Command?.Template);
        }

        var resolver = new PointMappingResolver();

        foreach (var point in source.Points)
        {
            if (!Identifier().IsMatch(point.Id) || string.IsNullOrWhiteSpace(point.Name))
            {
                throw new PointSourceValidationException("point id and name are required and must be valid");
            }

            PointMappingResolution resolution;

            try
            {
                resolution = resolver.Resolve(source, point);
            }
            catch (ArgumentException exception)
            {
                throw new PointSourceValidationException(exception.Message);
            }

            if (point.Readable && resolution.Mapping.Read is null)
            {
                throw new PointSourceValidationException($"readable point '{point.Id}' requires a read operation");
            }

            if (point.Commandable && resolution.Mapping.Command is null)
            {
                throw new PointSourceValidationException($"commandable point '{point.Id}' requires a command operation");
            }
        }
    }

    private static void ValidateMappingForKind(PointSourceKind kind, PointMapping mapping)
    {
        var valid = kind switch
        {
            PointSourceKind.Virtual => mapping.Virtual is not null && mapping.Physical is null
                && HasNoTransportFields(mapping),
            PointSourceKind.Physical => mapping.Physical is not null && mapping.Virtual is null
                && HasNoTransportFields(mapping),
            PointSourceKind.HomeAssistant => mapping.Physical is null && mapping.Virtual is null
                && (mapping.Read is null || !string.IsNullOrWhiteSpace(mapping.Read.EntityId))
                && (mapping.Command is null || !string.IsNullOrWhiteSpace(mapping.Command.Service)),
            PointSourceKind.Mqtt => mapping.Physical is null && mapping.Virtual is null
                && (mapping.Read is null || !string.IsNullOrWhiteSpace(mapping.Read.Topic))
                && (mapping.Command is null || !string.IsNullOrWhiteSpace(mapping.Command.Topic)),
            PointSourceKind.HttpJson => mapping.Physical is null && mapping.Virtual is null
                && (mapping.Read is null || IsHttpRead(mapping.Read))
                && (mapping.Command is null || IsHttpCommand(mapping.Command)),
            _ => false
        };

        if (!valid)
        {
            throw new PointSourceValidationException(
                $"mapping '{mapping.Id}' contains fields incompatible with source kind '{kind}'");
        }
    }

    private static bool HasNoTransportFields(PointMapping mapping) =>
        (mapping.Read is null || mapping.Read == new MappingReadOperation())
        && (mapping.Command is null || mapping.Command == new MappingCommandOperation());

    private static bool IsHttpRead(MappingReadOperation read) =>
        !string.IsNullOrWhiteSpace(read.Path)
        && read.Method is "GET" or "HEAD"
        && !string.IsNullOrWhiteSpace(read.Template);

    private static bool IsHttpCommand(MappingCommandOperation command) =>
        !string.IsNullOrWhiteSpace(command.Path)
        && command.Method is "POST" or "PUT" or "PATCH" or "DELETE"
        && !string.IsNullOrWhiteSpace(command.Template);

    private void ValidateTemplate(PointMapping mapping, string operation, string? template)
    {
        if (template is null)
        {
            return;
        }

        if (!template.Contains("{{", StringComparison.Ordinal))
        {
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(template);

                if (node is not System.Text.Json.Nodes.JsonObject output
                    || !output.Select(item => item.Key).Order().SequenceEqual(mapping.Aliases.Order()))
                {
                    throw new PointSourceValidationException(
                        $"mappings.{mapping.Id}.{operation}.template must render exactly the declared alias object");
                }
            }
            catch (System.Text.Json.JsonException)
            {
                throw new PointSourceValidationException(
                    $"mappings.{mapping.Id}.{operation}.template output is malformed");
            }
        }

        if (templates is null)
        {
            return;
        }

        var result = templates.Validate(template);

        if (result.IsValid)
        {
            return;
        }

        var diagnostic = result.Diagnostics[0];
        throw new PointSourceValidationException(
            $"mappings.{mapping.Id}.{operation}.template ({diagnostic.Line}:{diagnostic.Column}): {diagnostic.Message}");
    }

    private static void RejectDuplicateIds(IEnumerable<string> ids, string description)
    {
        if (ids.GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new PointSourceValidationException($"duplicate {description} id");
        }
    }

    private static void RejectDuplicateAliases(IReadOnlyList<string> aliases)
    {
        if (aliases.Count == 0 || aliases.Any(alias => !Alias().IsMatch(alias))
            || aliases.Distinct(StringComparer.Ordinal).Count() != aliases.Count)
        {
            throw new PointSourceValidationException("mapping aliases must be non-empty, valid, and unique");
        }
    }

    private static string RequireBaseUrl(PointSource source) =>
        string.IsNullOrEmpty(source.Connection.BaseUrl)
            ? throw new PointSourceValidationException("connection.baseUrl is required")
            : source.Connection.BaseUrl;

    private static string ValidateHttpJson(PointSource source)
    {
        var address = RequireBaseUrl(source);

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

        var allowedScheme = source.Kind == PointSourceKind.Mqtt
            ? uri.Scheme is "mqtt" or "mqtts"
            : uri.Scheme is "http" or "https";

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

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Alias();
}