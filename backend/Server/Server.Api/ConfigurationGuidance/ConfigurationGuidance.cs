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
            [typeof(AutomationPoint)] = H(("id", "Globally unique point identifier."), ("name", "Operator-facing display name."), ("description", "Optional explanation of what the point represents."), ("enabled", "Whether the point participates in runtime processing."), ("direction", "Permitted data direction."), ("valueType", "Logical type used to convert the mapping alias."), ("units", "Engineering units for analog and integer values."), ("stateLabels", "Labels for digital or multi-state values."), ("readable", "Whether clients and flows may read the value."), ("commandable", "Whether clients and flows may command the value."), ("persistence", "Runtime persistence: `volatile` or `retained`."), ("relinquishDefault", "Typed fallback used when no writer supplies a value."), ("mapping", "Strict `<mapping-id>/<alias>` reference owned by this source."), ("limits", "Optional type-specific value constraints."), ("safeDisablePolicy", "Required safe transition for commandable points.")),
            [typeof(PointSource)] = H(("schemaVersion", "Configuration format version; currently `1`."), ("id", "Stable lowercase, hyphen-separated source identifier."), ("name", "Operator-facing source name."), ("description", "Optional explanation of the source."), ("enabled", "Whether connections and point operations are allowed."), ("kind", "Integration kind: `virtual`, `physical`, `homeAssistant`, `mqtt`, or `httpJson`."), ("connection", "Kind-specific connection settings."), ("credentialRef", "Credential-store reference such as `secret://weather`."), ("tls", "TLS verification settings."), ("timeouts", "Connection and request timeout settings."), ("mappings", "Reusable source-local communication mappings."), ("points", "Point definitions owned by this source.")),
            [typeof(PointSourceConnection)] = H(("baseUrl", "HTTP base URL for Home Assistant or HTTP/JSON."), ("subscribeEvents", "Subscribe to Home Assistant events."), ("brokerUrl", "MQTT broker URL."), ("clientIdPrefix", "Prefix for generated MQTT client IDs."), ("testTopic", "Exact read-only MQTT topic used by connectivity tests."), ("qos", "MQTT quality-of-service level: `0`, `1`, or `2`."), ("cleanStart", "Start MQTT sessions without previous session state."), ("keepAliveSeconds", "MQTT keepalive interval."), ("defaultPollMilliseconds", "Default HTTP polling interval."), ("followRedirects", "Whether HTTP redirects may be followed."), ("maximumResponseBytes", "Maximum accepted HTTP response size."), ("allowPrivateNetwork", "Explicitly permit private-network destinations.")),
            [typeof(TlsOptions)] = H(("verifyServerCertificate", "Must remain `true`; insecure TLS is not supported.")),
            [typeof(PointSourceTimeouts)] = H(("connectMilliseconds", "Connection timeout in milliseconds."), ("requestMilliseconds", "Optional complete-request timeout in milliseconds.")),
            [typeof(PointMapping)] = H(("id", "Source-local mapping identifier."), ("aliases", "Case-sensitive values exposed by this mapping."), ("read", "Optional kind-specific read communication."), ("command", "Optional kind-specific command communication."), ("physical", "Physical controller/channel/address configuration."), ("virtual", "Virtual runtime behavior.")),
            [typeof(MappingReadOperation)] = H(("path", "Relative HTTP read path."), ("method", "Safe read method."), ("format", "Transport format."), ("template", "Template rendering an alias object from the response."), ("topic", "MQTT state topic."), ("qos", "MQTT read QoS."), ("entityId", "Home Assistant entity identifier."), ("property", "Home Assistant state property."), ("pollMilliseconds", "Optional mapping polling interval.")),
            [typeof(MappingCommandOperation)] = H(("path", "Relative HTTP command path."), ("method", "Command method."), ("format", "Transport format."), ("contentType", "Command content type."), ("template", "Template rendering the transport command."), ("topic", "MQTT command topic."), ("qos", "MQTT command QoS."), ("retain", "Whether MQTT retains the command."), ("service", "Home Assistant service."), ("serviceData", "Home Assistant service payload.")),
            [typeof(HomeAssistantPointMapping)] = H(("entityId", "Home Assistant entity ID."), ("stateProperty", "Optional property read from the entity state."), ("commandService", "Optional Home Assistant service used for commands.")),
            [typeof(MqttPointMapping)] = H(("stateTopic", "Topic subscribed to for readable values."), ("commandTopic", "Topic used for commands."), ("qos", "MQTT quality-of-service level."), ("retain", "Whether command publications use MQTT retain."), ("jsonPointer", "Optional JSON Pointer selecting the value from a payload.")),
            [typeof(HttpJsonPointMapping)] = H(("path", "Required path relative to the source base URL."), ("method", "Command method (`POST`, `PUT`, or `PATCH`) for commandable points; otherwise `GET` or `HEAD`. Readback always uses a safe read request."), ("jsonPointer", "Optional JSON Pointer selecting the value from a read response."), ("valuePointer", "Optional JSON Pointer where the commanded value is placed in the request body."), ("contentType", "Optional command body type: `application/json` (default) or `application/x-www-form-urlencoded`.")),
            [typeof(ControllerTemplate)] = H(("schemaVersion", "Configuration format version; currently `1`."), ("id", "Stable template identifier."), ("name", "Operator-facing template name."), ("description", "Optional target description."), ("readOnly", "Whether users may modify this template."), ("capabilities", "Functions and point features supported by the target."), ("limits", "Target resource limits.")),
            [typeof(ControllerCapabilities)] = H(("pointTypes", "Supported point value types."), ("pointDirections", "Supported point directions."), ("pointFeatures", "Supported point features."), ("connectorDataTypes", "Supported flow connector data types."), ("flowFunctions", "Supported flow functions."), ("executionModes", "Supported execution modes."), ("runtimeFeatures", "Supported runtime features.")),
            [typeof(ControllerLimits)] = H(("maxFlows", "Maximum deployed flows."), ("maxNodesPerFlow", "Maximum nodes in one flow."), ("maxConnectionsPerFlow", "Maximum connections in one flow."), ("minimumIntervalMilliseconds", "Smallest supported interval execution period."))
        };

    public static string Render(
        string configurationType,
        ReadOnlySpan<byte> yaml,
        string? selectedPointId = null)
    {
        var kind = configurationType switch
        {
            "point-source" => ConfigurationKind.PointSources,
            "controller-template" => ConfigurationKind.Controller,
            _ => throw new ArgumentException("unknown configuration type", nameof(configurationType))
        };

        var current = ConfigurationYaml.ParseToJson(yaml, kind);

        Type[] types = configurationType switch
        {
            "point-source" => [
                typeof(PointSource),
                typeof(PointSourceConnection),
                typeof(TlsOptions),
                typeof(PointSourceTimeouts),
                typeof(PointMapping),
                typeof(MappingReadOperation),
                typeof(MappingCommandOperation),
                typeof(AutomationPoint)
            ],

            _ => [
                typeof(ControllerTemplate),
                typeof(ControllerCapabilities),
                typeof(ControllerLimits)
            ]
        };

        var markdown = new StringBuilder();

        markdown.Append($"# {Title(configurationType)} YAML guidance\n\nGenerated from the server's current C# configuration contract.\n\n");

        foreach (var type in types)
        {
            AppendType(markdown, type);
        }

        if (configurationType == "point-source" && !string.IsNullOrWhiteSpace(selectedPointId))
        {
            RenderSelectedPoint(markdown, current, selectedPointId);
        }

        markdown.Append("## YAML structure for this selection\n\n```yaml\n");
        markdown.Append(Encoding.UTF8.GetString(yaml).Trim());
        markdown.Append("\n```\n");

        return markdown.ToString();
    }

    private static void RenderSelectedPoint(StringBuilder markdown, JsonNode current, string pointId)
    {
        var point = current["points"]?.AsArray().OfType<JsonObject>()
            .SingleOrDefault(candidate => candidate["id"]?.GetValue<string>() == pointId);
        ArgumentNullException.ThrowIfNull(point, $"point '{pointId}' is not present in the aggregate");
        markdown.Append($"## Selected point `{pointId}`\n\nValue type: `{point["valueType"]}`. Resolved mapping: `{point["mapping"]}`.\n\n");
    }

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