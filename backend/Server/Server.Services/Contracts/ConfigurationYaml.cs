using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Server.Services.Contracts;

public static class ConfigurationYaml
{
    public const int MaximumBytes = 256 << 10;
    public const int MaximumDepth = 20;

    private static readonly IReadOnlySet<string> PointRootFields =
        new HashSet<string>(["schemaVersion", "groups", "points"]);
    private static readonly IReadOnlySet<string> PointGroupFields =
        new HashSet<string>(["id", "name", "description", "sourceId", "mappingDefaults"]);
    private static readonly IReadOnlySet<string> PointFields = new HashSet<string>(
        [
            "id", "name", "description", "enabled", "groupId", "implementation", "direction",
            "valueType", "units", "stateLabels", "readable", "commandable", "persistence",
            "relinquishDefault", "sourceId", "mapping", "limits", "safeDisablePolicy",
        ]);
    private static readonly IReadOnlySet<string> SourceRootFields =
        new HashSet<string>(["schemaVersion", "sources"]);
    private static readonly IReadOnlySet<string> SourceFields = new HashSet<string>(
        [
            "id", "name", "description", "enabled", "kind", "connection", "credentialRef",
            "tls", "timeouts",
        ]);
    private static readonly IReadOnlySet<string> SourceConnectionFields = new HashSet<string>(
        [
            "baseUrl", "subscribeEvents", "brokerUrl", "clientIdPrefix", "testTopic", "qos",
            "cleanStart", "keepAliveSeconds", "allowedReadMethods", "defaultPollMilliseconds",
            "followRedirects", "maximumResponseBytes", "allowPrivateNetwork",
        ]);
    private static readonly IReadOnlySet<string> TlsFields =
        new HashSet<string>(["verifyServerCertificate"]);
    private static readonly IReadOnlySet<string> TimeoutFields =
        new HashSet<string>(["connectMilliseconds", "requestMilliseconds"]);
    private static readonly IReadOnlySet<string> ControllerFields = new HashSet<string>(
        ["schemaVersion", "id", "name", "description", "readOnly", "capabilities", "limits"]);
    private static readonly IReadOnlySet<string> CapabilityFields = new HashSet<string>(
        [
            "pointTypes", "pointDirections", "pointFeatures", "connectorDataTypes",
            "flowFunctions", "executionModes", "runtimeFeatures",
        ]);
    private static readonly IReadOnlySet<string> ControllerLimitFields = new HashSet<string>(
        [
            "maxFlows", "maxNodesPerFlow", "maxConnectionsPerFlow",
            "minimumIntervalMilliseconds",
        ]);

    public static JsonObject Parse(
        ReadOnlySpan<byte> yaml,
        ConfigurationKind kind,
        int maximumBytes = MaximumBytes)
    {
        if (yaml.Length > maximumBytes)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.TooLarge,
                "YAML exceeds 256 KiB limit.");
        }

        var text = Encoding.UTF8.GetString(yaml);
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException exception)
        {
            var category = exception.Message.Contains(
                "duplicate",
                StringComparison.OrdinalIgnoreCase)
                ? ConfigurationYamlError.UnsupportedFeature
                : ConfigurationYamlError.Syntax;
            throw new ConfigurationYamlException(
                category,
                "Invalid YAML.",
                exception);
        }

        if (stream.Documents.Count == 0)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.Empty,
                "YAML document is empty.");
        }

        if (stream.Documents.Count != 1)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.MultipleDocuments,
                "YAML must contain one document.");
        }

        var rootNode = stream.Documents[0].RootNode;
        ValidateSyntax(rootNode, 0, new HashSet<YamlNode>(ReferenceEqualityComparer.Instance));
        var root = ConvertNode(rootNode) as JsonObject
            ?? throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML root must be a mapping.");

        if (root["schemaVersion"]?.GetValue<long>() != 1)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.UnsupportedSchema,
                "schemaVersion must be 1.");
        }

        ValidateContract(root, kind);
        return root;
    }

    public static T Parse<T>(ReadOnlySpan<byte> yaml, ConfigurationKind kind)
    {
        var document = Parse(yaml, kind);
        return document.Deserialize<T>(FlowControlJson.Options)
            ?? throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML does not match the requested contract.");
    }

    public static string Render<T>(T value)
    {
        var json = JsonSerializer.SerializeToNode(value, FlowControlJson.Options);
        return new SerializerBuilder()
            .JsonCompatible()
            .DisableAliases()
            .Build()
            .Serialize(ToYamlValue(json));
    }

    private static void ValidateSyntax(
        YamlNode node,
        int depth,
        HashSet<YamlNode> visited)
    {
        if (depth > MaximumDepth)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.ExcessiveNesting,
                "YAML nesting exceeds 20 levels.");
        }

        if (!node.Anchor.IsEmpty)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.UnsupportedFeature,
                "YAML aliases and anchors are unsupported.");
        }

        if (!visited.Add(node))
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.UnsupportedFeature,
                "YAML aliases and anchors are unsupported.");
        }

        var tag = node.Tag.ToString();
        if (tag is not "" and not "?" and not "!"
            && !tag.StartsWith("tag:yaml.org,2002:", StringComparison.Ordinal))
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.UnsupportedFeature,
                "Custom YAML tags are unsupported.");
        }

        if (node is YamlMappingNode mapping)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in mapping.Children)
            {
                var key = GetKey(entry.Key);
                if (!keys.Add(key))
                {
                    throw new ConfigurationYamlException(
                        ConfigurationYamlError.UnsupportedFeature,
                        $"Duplicate YAML key \"{key}\".");
                }

                ValidateSyntax(entry.Key, depth + 1, visited);
                ValidateSyntax(entry.Value, depth + 1, visited);
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (var child in sequence.Children)
            {
                ValidateSyntax(child, depth + 1, visited);
            }
        }
    }

    private static JsonNode? ConvertNode(YamlNode node)
    {
        return node switch
        {
            YamlMappingNode mapping => ConvertMapping(mapping),
            YamlSequenceNode sequence => new JsonArray(
                sequence.Children.Select(ConvertNode).ToArray()),
            YamlScalarNode scalar => ConvertScalar(scalar),
            _ => throw new ConfigurationYamlException(
                ConfigurationYamlError.UnsupportedFeature,
                "Unsupported YAML node."),
        };
    }

    private static object? ToYamlValue(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonObject value => value.ToDictionary(
                item => item.Key,
                item => ToYamlValue(item.Value)),
            JsonArray items => items.Select(ToYamlValue).ToList(),
            JsonValue value => ToScalarValue(value),
            _ => throw new InvalidOperationException("Unsupported JSON node."),
        };
    }

    private static object? ToScalarValue(JsonValue value)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        var element = document.RootElement;
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException("Unsupported JSON scalar."),
        };
    }

    private static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var result = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            result.Add(GetKey(entry.Key), ConvertNode(entry.Value));
        }

        return result;
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        var tag = scalar.Tag.ToString();
        if (tag == "tag:yaml.org,2002:null")
        {
            return null;
        }

        if (tag == "tag:yaml.org,2002:bool")
        {
            return JsonValue.Create(bool.Parse(value));
        }

        if (tag == "tag:yaml.org,2002:int")
        {
            return JsonValue.Create(
                long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        }

        if (tag == "tag:yaml.org,2002:float")
        {
            return JsonValue.Create(
                double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        if (scalar.Style != ScalarStyle.Plain || tag == "tag:yaml.org,2002:str")
        {
            return JsonValue.Create(value);
        }

        if (value is "~" or "null" or "Null" or "NULL")
        {
            return null;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return JsonValue.Create(boolean);
        }

        if (long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var integer))
        {
            return JsonValue.Create(integer);
        }

        if (double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number))
        {
            return JsonValue.Create(number);
        }

        return JsonValue.Create(value);
    }

    private static string GetKey(YamlNode node)
    {
        if (node is not YamlScalarNode { Value: not null } scalar)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML mapping keys must be strings.");
        }

        return scalar.Value;
    }

    private static void ValidateContract(JsonObject root, ConfigurationKind kind)
    {
        switch (kind)
        {
            case ConfigurationKind.Points:
                RejectUnknown(root, PointRootFields);
                ValidateItems(root["groups"], PointGroupFields, "groups");
                ValidateItems(root["points"], PointFields, "points");
                break;
            case ConfigurationKind.PointSources:
                RejectUnknown(root, SourceRootFields);
                ValidateItems(root["sources"], SourceFields, "sources");
                if (root["sources"] is JsonArray sources)
                {
                    foreach (var source in sources.OfType<JsonObject>())
                    {
                        ValidateObject(source["connection"], SourceConnectionFields, "connection");
                        ValidateObject(source["tls"], TlsFields, "tls");
                        ValidateObject(source["timeouts"], TimeoutFields, "timeouts");
                    }
                }

                break;
            case ConfigurationKind.Controller:
                RejectUnknown(root, ControllerFields);
                ValidateObject(root["capabilities"], CapabilityFields, "capabilities");
                ValidateObject(root["limits"], ControllerLimitFields, "limits");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static void ValidateItems(
        JsonNode? node,
        IReadOnlySet<string> fields,
        string name)
    {
        if (node is not JsonArray items)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                $"{name} must be a sequence.");
        }

        foreach (var item in items)
        {
            if (item is not JsonObject value)
            {
                throw new ConfigurationYamlException(
                    ConfigurationYamlError.InvalidShape,
                    $"{name} entries must be mappings.");
            }

            RejectUnknown(value, fields);
        }
    }

    private static void ValidateObject(
        JsonNode? node,
        IReadOnlySet<string> fields,
        string name)
    {
        if (node is not JsonObject value)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                $"{name} must be a mapping.");
        }

        RejectUnknown(value, fields);
    }

    private static void RejectUnknown(JsonObject value, IReadOnlySet<string> allowed)
    {
        foreach (var field in value)
        {
            if (!allowed.Contains(field.Key))
            {
                throw new ConfigurationYamlException(
                    ConfigurationYamlError.UnknownField,
                    $"Unknown field \"{field.Key}\".");
            }
        }
    }
}