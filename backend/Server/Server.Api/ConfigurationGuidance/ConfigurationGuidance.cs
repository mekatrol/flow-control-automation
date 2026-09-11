using Server.Common.Models.Communication;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.Api.ConfigurationGuidance;

public static class ConfigurationGuidance
{
    private static readonly Dictionary<Type, Dictionary<string, string>> Help =
        new Dictionary<Type, Dictionary<string, string>>
        {
            [typeof(PointDocument)] = H(("schemaVersion", "Configuration format version; currently `1`."), ("points", "Point definitions in this document.")),
            [typeof(AutomationPoint)] = H(("id", "Stable lowercase, hyphen-separated identifier."), ("name", "Operator-facing display name."), ("description", "Optional explanation of what the point represents."), ("enabled", "Whether the point participates in runtime processing."), ("direction", "Permitted data direction."), ("valueType", "Logical type of the point value."), ("pointSourceType", "Whether the value is virtual, physical, or remote."), ("units", "Engineering units for analog and integer values."), ("stateLabels", "Labels for digital or multi-state values."), ("readable", "Whether clients and flows may read the value."), ("commandable", "Whether clients and flows may command the value."), ("persistence", "Runtime persistence: `volatile` or `retained`."), ("relinquishDefault", "Typed fallback used when no writer supplies a value."), ("sourceId", "Remote point-source identifier."), ("mapping", "Source-relative address and read/write settings."), ("limits", "Optional type-specific value constraints."), ("safeDisablePolicy", "Required transition behavior for commandable non-virtual points.")),
            [typeof(PointSourceDocument)] = H(("schemaVersion", "Configuration format version; currently `1`."), ("sources", "Point sources in this document.")),
            [typeof(PointSource)] = H(("id", "Stable lowercase, hyphen-separated source identifier."), ("name", "Operator-facing source name."), ("description", "Optional explanation of the external system."), ("enabled", "Whether connections and point operations are allowed."), ("kind", "Integration kind: `homeAssistant`, `mqtt`, or `httpJson`."), ("connection", "Kind-specific connection settings."), ("credentialRef", "Credential-store reference such as `secret://weather`."), ("tls", "TLS verification settings."), ("timeouts", "Connection and request timeout settings.")),
            [typeof(PointSourceConnection)] = H(("baseUrl", "HTTP base URL for Home Assistant or HTTP/JSON."), ("subscribeEvents", "Subscribe to Home Assistant events."), ("brokerUrl", "MQTT broker URL."), ("clientIdPrefix", "Prefix for generated MQTT client IDs."), ("testTopic", "Exact read-only MQTT topic used by connectivity tests."), ("qos", "MQTT quality-of-service level: `0`, `1`, or `2`."), ("cleanStart", "Start MQTT sessions without previous session state."), ("keepAliveSeconds", "MQTT keepalive interval."), ("defaultPollMilliseconds", "Default HTTP polling interval."), ("followRedirects", "Whether HTTP redirects may be followed."), ("maximumResponseBytes", "Maximum accepted HTTP response size."), ("allowPrivateNetwork", "Explicitly permit private-network MQTT destinations.")),
            [typeof(TlsOptions)] = H(("verifyServerCertificate", "Must remain `true`; insecure TLS is not supported.")),
            [typeof(PointSourceTimeouts)] = H(("connectMilliseconds", "Connection timeout in milliseconds."), ("requestMilliseconds", "Optional complete-request timeout in milliseconds.")),
            [typeof(HomeAssistantPointMapping)] = H(("entityId", "Home Assistant entity ID."), ("stateProperty", "Optional property read from the entity state."), ("commandService", "Optional Home Assistant service used for commands.")),
            [typeof(MqttPointMapping)] = H(("stateTopic", "Topic subscribed to for readable values."), ("commandTopic", "Topic used for commands."), ("qos", "MQTT quality-of-service level."), ("retain", "Whether command publications use MQTT retain."), ("jsonPointer", "Optional JSON Pointer selecting the value from a payload.")),
            [typeof(HttpJsonPointMapping)] = H(("path", "Required path relative to the source base URL."), ("method", "HTTP method used for this point."), ("jsonPointer", "Optional JSON Pointer selecting the value from the response.")),
            [typeof(ControllerTemplate)] = H(("schemaVersion", "Configuration format version; currently `1`."), ("id", "Stable template identifier."), ("name", "Operator-facing template name."), ("description", "Optional target description."), ("readOnly", "Whether users may modify this template."), ("capabilities", "Functions and point features supported by the target."), ("limits", "Target resource limits.")),
            [typeof(ControllerCapabilities)] = H(("pointTypes", "Supported point value types."), ("pointDirections", "Supported point directions."), ("pointFeatures", "Supported point features."), ("connectorDataTypes", "Supported flow connector data types."), ("flowFunctions", "Supported flow functions."), ("executionModes", "Supported execution modes."), ("runtimeFeatures", "Supported runtime features.")),
            [typeof(ControllerLimits)] = H(("maxFlows", "Maximum deployed flows."), ("maxNodesPerFlow", "Maximum nodes in one flow."), ("maxConnectionsPerFlow", "Maximum connections in one flow."), ("minimumIntervalMilliseconds", "Smallest supported interval execution period."))
        };

    public static string Render(
        string configurationType,
        ReadOnlySpan<byte> yaml,
        string? pointSourceKind = null)
    {
        var kind = configurationType switch
        {
            "point" => ConfigurationKind.Points,
            "point-source" => ConfigurationKind.PointSources,
            "controller-template" => ConfigurationKind.Controller,
            _ => throw new ArgumentException("unknown configuration type", nameof(configurationType))
        };

        var current = ConfigurationYaml.ParseToJson(yaml, kind);
        var types = configurationType switch
        {
            "point" => new[] { typeof(PointDocument), typeof(AutomationPoint) },
            "point-source" => new[] { typeof(PointSourceDocument), typeof(PointSource), typeof(PointSourceConnection), typeof(TlsOptions), typeof(PointSourceTimeouts) },
            _ => new[] { typeof(ControllerTemplate), typeof(ControllerCapabilities), typeof(ControllerLimits) }
        };

        var markdown = new StringBuilder();

        if (configurationType == "point")
        {
            RenderPoint(markdown, current, pointSourceKind);
        }
        else
        {
            markdown.Append($"# {Title(configurationType)} YAML guidance\n\nGenerated from the server's current C# configuration contract.\n\n");

            foreach (var type in types)
            {
                AppendType(markdown, type);
            }
        }

        markdown.Append("## YAML structure for this selection\n\n```yaml\n");
        markdown.Append(configurationType == "point" ? PointSample(current, pointSourceKind) : Encoding.UTF8.GetString(yaml).Trim());
        markdown.Append("\n```\n");

        return markdown.ToString();
    }

    private static void RenderPoint(
        StringBuilder markdown,
        JsonNode current,
        string? pointSourceKind)
    {
        var point = current["points"]?[0];
        var sourceType = StringValue(point?["pointSourceType"]) ?? "point";
        var valueType = StringValue(point?["valueType"]) ?? "point";
        var direction = StringValue(point?["direction"]) ?? "value";
        var commandable = BoolValue(point?["commandable"]);
        var title = $"{Title(valueType)} {Title(direction)} {Title(sourceType)} point";
        markdown.Append($"# {title} YAML guidance\n\nOnly fields applicable to the selected point type are shown. This is generated from the server's current C# configuration contract.\n\n");
        AppendType(markdown, typeof(PointDocument));

        var fields = new HashSet<string>(["id", "name", "description", "enabled", "direction", "valueType", "pointSourceType", "readable", "commandable", "persistence"], StringComparer.Ordinal);

        if (valueType is "analog" or "integer")
        {
            fields.Add("units");
        }

        if (valueType is "digital" or "multiState")
        {
            fields.Add("stateLabels");
        }

        if (valueType is "analog" or "integer" or "text")
        {
            fields.Add("limits");
        }

        if (sourceType == "virtual")
        {
            fields.Add("relinquishDefault");
        }

        if (sourceType == "remote")
        {
            fields.Add("sourceId");
            fields.Add("mapping");
        }

        if (sourceType != "virtual" && commandable)
        {
            fields.Add("safeDisablePolicy");
        }

        AppendType(markdown, typeof(AutomationPoint), fields);

        if (sourceType == "remote")
        {
            var mappingType = MappingType(pointSourceKind);

            if (mappingType is null)
            {
                markdown.Append("## Mapping\n\nThe referenced point source could not be resolved, so its mapping fields cannot be shown.\n\n");
            }
            else
            {
                markdown.Append($"## {Title(pointSourceKind!)} mapping\n\nThese are the fields accepted by the referenced `{pointSourceKind}` point source.\n\n");
                AppendType(markdown, mappingType);
            }
        }
    }

    private static string PointSample(JsonNode current, string? pointSourceKind)
    {
        var sample = current.DeepClone();
        var point = sample["points"]?[0];

        if (point is not null && StringValue(point["pointSourceType"]) == "remote")
        {
            point["mapping"] = pointSourceKind switch
            {
                "homeAssistant" => new JsonObject { ["entityId"] = "binary_sensor.example", ["stateProperty"] = "state" },
                "mqtt" => new JsonObject { ["stateTopic"] = "devices/example/state", ["qos"] = 1, ["retain"] = false },
                "httpJson" => new JsonObject { ["path"] = "/points/example", ["method"] = "GET", ["jsonPointer"] = "/value" },
                _ => point["mapping"]?.DeepClone()
            };
        }

        return ConfigurationYaml.Render(sample).Trim();
    }

    private static Type? MappingType(string? pointSourceKind) => pointSourceKind switch
    {
        "homeAssistant" => typeof(HomeAssistantPointMapping),
        "mqtt" => typeof(MqttPointMapping),
        "httpJson" => typeof(HttpJsonPointMapping),
        _ => null
    };

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;

    private static bool BoolValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var result) && result;

    public static IReadOnlyList<string> CoverageErrors()
    {
        var errors = new List<string>();

        foreach (var entry in Help)
        {
            var fields = ConfigurationProperties(entry.Key).Select(JsonName).ToHashSet(StringComparer.Ordinal);

            foreach (var missing in fields.Except(entry.Value.Keys))
            {
                errors.Add($"{entry.Key.Name}.{missing} has no contextual help.");
            }

            foreach (var stale in entry.Value.Keys.Except(fields))
            {
                errors.Add($"{entry.Key.Name}.{stale} has contextual help but no configuration field.");
            }
        }

        return errors;
    }

    private static void AppendType(StringBuilder markdown, Type type, HashSet<string>? included = null)
    {
        markdown.Append($"### {Split(type.Name)}\n\n| Field | Type | Guidance |\n| --- | --- | --- |\n");

        foreach (var property in ConfigurationProperties(type).Where(property => included is null || included.Contains(JsonName(property))))
        {
            var name = JsonName(property);
            markdown.Append($"| `{name}` | `{DisplayType(property.PropertyType)}` | {Help[type][name]} |\n");
        }

        markdown.Append('\n');
    }

    private static IEnumerable<PropertyInfo> ConfigurationProperties(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod is not null && p.GetCustomAttribute<JsonIgnoreAttribute>() is null && p.Name is not ("Revision" or "CreatedAt" or "UpdatedAt"));
    private static string JsonName(PropertyInfo property) => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);
    private static string DisplayType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsEnum)
        {
            return string.Join(", ", Enum.GetNames(type).Select(JsonNamingPolicy.CamelCase.ConvertName));
        }

        if (type.IsArray
            || type.IsGenericType
            && type.GetGenericTypeDefinition() is var genericType
            && (genericType == typeof(IReadOnlyList<>)
                || genericType == typeof(IList<>)
                || genericType == typeof(List<>)))
        {
            return "array";
        }

        return type == typeof(string) ? "string" : type == typeof(bool) ? "boolean" : type.IsPrimitive ? "number" : "object";
    }

    private static string Title(string value) => string.Join(' ', value.Split('-').Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    private static string Split(string value) => System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
    private static Dictionary<string, string> H(params (string Key, string Value)[] values) => values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
}
