using Server.Services.Contracts;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

public sealed partial class PointDefinitionValidator : IPointDefinitionValidator
{
    public const string ReservedStandaloneGroupName = "__StandalonePointGroup__";
    private const long MaximumSafeInteger = 9_007_199_254_740_991;

    public ValidatedPointDefinition Validate(
        Point point,
        PointValidationContext context)
    {
        ValidateIdentity(point.Id, point.Name, "point");

        var implementation = ParseImplementation(point.Implementation);
        var direction = ParseDirection(point.Direction);
        var valueType = ParseValueType(point.ValueType);
        var persistence = ParsePersistence(point.Persistence);
        ValidateCapabilities(point, implementation, direction);

        var limits = ParseLimits(point.Limits, valueType);
        var (digitalLabels, multiStateLabels) = ParseLabels(point.StateLabels, valueType);
        ValidateUnits(point.Units, valueType);
        ValidateValue(point.RelinquishDefault, valueType, limits, multiStateLabels,
            "relinquishDefault", required: implementation == PointImplementation.Virtual
                && persistence == PointPersistence.Retained);

        var (sourceKind, mapping) = ValidateBinding(point, implementation, context);
        var safety = ParseSafetyPolicy(
            point.SafeDisablePolicy,
            point.Commandable && implementation == PointImplementation.Bound);

        return new ValidatedPointDefinition(
            point, implementation, direction, valueType, persistence, limits,
            digitalLabels, multiStateLabels, safety, sourceKind, mapping);
    }

    public void ValidateGroup(
        PointGroup group,
        IReadOnlyDictionary<string, PointSource> sources)
    {
        ValidateIdentity(group.Id, group.Name, "group");
        if (string.Equals(
            group.Name.Trim(),
            ReservedStandaloneGroupName,
            StringComparison.OrdinalIgnoreCase))
        {
            Fail($"group name \"{ReservedStandaloneGroupName}\" is reserved");
        }

        if (string.IsNullOrWhiteSpace(group.SourceId))
        {
            if (group.MappingDefaults.Count != 0)
            {
                Fail("group mappingDefaults require sourceId");
            }

            return;
        }

        if (!sources.ContainsKey(group.SourceId))
        {
            Fail($"group sourceId \"{group.SourceId}\" does not exist");
        }

        RejectCredentialLiterals(group.MappingDefaults, "mappingDefaults");
    }

    public void ValidateDocument(
        PointDocument document,
        IReadOnlyDictionary<string, PointSource> sources)
    {
        if (document.SchemaVersion != 1)
        {
            Fail("schemaVersion must be 1");
        }

        RejectDuplicates(document.Groups.Select(group => group.Id), "group id");
        RejectDuplicates(document.Groups.Select(group => group.Name), "group name");
        RejectDuplicates(document.Points.Select(point => point.Id), "point id");
        RejectDuplicates(document.Points.Select(point => point.Name), "point name");

        foreach (var group in document.Groups)
        {
            ValidateGroup(group, sources);
        }

        var groups = document.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        var context = new PointValidationContext(groups, sources);
        foreach (var point in document.Points)
        {
            Validate(point, context);
        }
    }

    private static void ValidateIdentity(string id, string name, string kind)
    {
        if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || !IdentifierRegex().IsMatch(id))
        {
            Fail($"{kind} id must be a lowercase identifier");
        }

        if (string.IsNullOrWhiteSpace(name) || name != name.Trim())
        {
            Fail($"{kind} name must be non-empty without surrounding whitespace");
        }
    }

    private static void ValidateCapabilities(
        Point point,
        PointImplementation implementation,
        PointDirection direction)
    {
        if (implementation == PointImplementation.Virtual && direction != PointDirection.Value)
        {
            Fail("virtual points must use value direction");
        }

        if (implementation == PointImplementation.Bound && direction == PointDirection.Value)
        {
            Fail("bound points cannot use value direction");
        }

        if (point.Commandable && !PointCompatibility.CanCommand(direction))
        {
            Fail($"{point.Direction} points cannot be commandable");
        }

        if (direction == PointDirection.Input && (!point.Readable || point.Commandable))
        {
            Fail("input points must be readable and not commandable");
        }

        if (direction == PointDirection.Output && !point.Commandable)
        {
            Fail("output points must be commandable");
        }

        if (direction == PointDirection.InputOutput && (!point.Readable || !point.Commandable))
        {
            Fail("input_output points must be readable and commandable");
        }

        if (direction == PointDirection.Value && !point.Readable && !point.Commandable)
        {
            Fail("value points must be readable or commandable");
        }
    }

    private static (PointSourceKind? Kind, PointMapping? Mapping) ValidateBinding(
        Point point,
        PointImplementation implementation,
        PointValidationContext context)
    {
        if (implementation == PointImplementation.Virtual)
        {
            if (point.SourceId is not null || point.Mapping is not null)
            {
                Fail("virtual points cannot have a source or mapping");
            }

            if (point.GroupId is not null)
            {
                if (!context.Groups.TryGetValue(point.GroupId, out var virtualGroup))
                {
                    Fail($"groupId \"{point.GroupId}\" does not exist");
                }

                if (virtualGroup!.SourceId is not null
                    || virtualGroup.MappingDefaults.Count != 0)
                {
                    Fail("virtual points cannot join a source-bound group");
                }
            }

            return (null, null);
        }

        PointGroup? group = null;
        if (point.GroupId is not null
            && !context.Groups.TryGetValue(point.GroupId, out group))
        {
            Fail($"groupId \"{point.GroupId}\" does not exist");
        }

        var inheritedSourceId = group?.SourceId;
        if (point.SourceId is not null && inheritedSourceId is not null
            && !string.Equals(point.SourceId, inheritedSourceId, StringComparison.Ordinal))
        {
            Fail("point sourceId conflicts with its group sourceId");
        }

        var sourceId = point.SourceId ?? inheritedSourceId;
        if (sourceId is null)
        {
            Fail("bound point requires an existing direct or inherited source");
        }

        if (!context.Sources.TryGetValue(sourceId!, out var source))
        {
            Fail($"sourceId \"{sourceId}\" does not exist");
        }

        var pointMapping = point.Mapping;
        if (pointMapping is null)
        {
            Fail("bound point requires mapping");
        }

        RejectCredentialLiterals(pointMapping!, "mapping");
        var kind = ParseSourceKind(source!.Kind);
        return (kind, ParseMapping(point, kind, pointMapping!));
    }

    private static PointMapping ParseMapping(
        Point point,
        PointSourceKind kind,
        JsonObject mapping) =>
        kind switch
        {
            PointSourceKind.HomeAssistant => new HomeAssistantPointMapping(
                RequiredString(mapping, "entityId"),
                OptionalString(mapping, "stateProperty"),
                point.Commandable
                    ? RequiredString(mapping, "commandService")
                    : OptionalString(mapping, "commandService")),
            PointSourceKind.Mqtt => ParseMqttMapping(point, mapping),
            PointSourceKind.HttpJson => ParseHttpMapping(point, mapping),
            _ => throw new InvalidOperationException("Unsupported source kind."),
        };

    private static MqttPointMapping ParseMqttMapping(Point point, JsonObject mapping)
    {
        var stateTopic = point.Readable
            ? RequiredString(mapping, "stateTopic")
            : OptionalString(mapping, "stateTopic");
        var commandTopic = point.Commandable
            ? RequiredString(mapping, "commandTopic")
            : OptionalString(mapping, "commandTopic");
        var qos = OptionalInteger(mapping, "qos") ?? 0;
        if (qos is < 0 or > 2)
        {
            Fail("mapping.qos must be 0, 1, or 2");
        }

        return new MqttPointMapping(
            stateTopic,
            commandTopic,
            qos,
            OptionalBoolean(mapping, "retain") ?? false,
            OptionalString(mapping, "jsonPointer"));
    }

    private static HttpJsonPointMapping ParseHttpMapping(Point point, JsonObject mapping)
    {
        var path = RequiredString(mapping, "path");
        if (!path.StartsWith('/')
            || path.StartsWith("//", StringComparison.Ordinal)
            || path.Contains("://", StringComparison.Ordinal))
        {
            Fail("mapping.path must be a relative absolute-path starting with /");
        }

        var method = (OptionalString(mapping, "method") ?? "GET").ToUpperInvariant();
        if (point.Readable && method is not "GET" and not "HEAD")
        {
            Fail("readable HTTP mappings must use GET or HEAD");
        }

        if (point.Commandable)
        {
            Fail("HTTP/JSON output mappings are not enabled in the initial release");
        }

        return new HttpJsonPointMapping(path, method, OptionalString(mapping, "jsonPointer"));
    }

    private static PointLimits? ParseLimits(JsonObject? value, PointValueType type)
    {
        if (value is null)
        {
            if (type == PointValueType.Text)
            {
                Fail("text points require limits.maximumLength");
            }

            return null;
        }

        var minimum = OptionalFiniteNumber(value, "minimum");
        var maximum = OptionalFiniteNumber(value, "maximum");
        var maximumLength = OptionalInteger(value, "maximumLength");
        if (minimum > maximum)
        {
            Fail("limits.minimum cannot exceed limits.maximum");
        }

        if (type == PointValueType.Integer)
        {
            ValidateSafeWholeNumber(minimum, "limits.minimum");
            ValidateSafeWholeNumber(maximum, "limits.maximum");
        }

        if (type is not PointValueType.Analog and not PointValueType.Integer
            && (minimum is not null || maximum is not null))
        {
            Fail("limits.minimum and limits.maximum apply only to numeric points");
        }

        if (type == PointValueType.Text && maximumLength is not > 0)
        {
            Fail("text points require a positive limits.maximumLength");
        }

        if (type != PointValueType.Text && maximumLength is not null)
        {
            Fail("limits.maximumLength applies only to text points");
        }

        return new PointLimits(minimum, maximum, maximumLength);
    }

    private static (DigitalStateLabels?, IReadOnlyList<MultiStateLabel>?) ParseLabels(
        JsonNode? value,
        PointValueType type)
    {
        if (type == PointValueType.Digital)
        {
            var labels = value as JsonObject
                ?? throw new PointDefinitionValidationException(
                    "digital points require stateLabels with false and true labels");
            var falseLabel = RequiredString(labels, "false");
            var trueLabel = RequiredString(labels, "true");
            if (labels.Count != 2 || string.Equals(
                falseLabel, trueLabel, StringComparison.OrdinalIgnoreCase))
            {
                Fail("digital stateLabels must contain two unique labels");
            }

            return (new DigitalStateLabels(falseLabel, trueLabel), null);
        }

        if (type == PointValueType.MultiState)
        {
            var items = value as JsonArray
                ?? throw new PointDefinitionValidationException(
                    "multi_state points require stateLabels");
            var labels = items.Select((item, index) =>
            {
                var entry = item as JsonObject
                    ?? throw new PointDefinitionValidationException(
                        $"stateLabels[{index}] must be an object");
                return new MultiStateLabel(
                    RequiredString(entry, "key"),
                    RequiredString(entry, "label"));
            }).ToArray();
            if (labels.Length < 2)
            {
                Fail("multi_state points require at least two states");
            }

            RejectDuplicates(labels.Select(label => label.Key), "state key");
            RejectDuplicates(labels.Select(label => label.Label), "state label");
            return (null, labels);
        }

        if (value is not null)
        {
            Fail("stateLabels apply only to digital and multi_state points");
        }

        return (null, null);
    }

    private static void ValidateValue(
        JsonNode? value,
        PointValueType type,
        PointLimits? limits,
        IReadOnlyList<MultiStateLabel>? states,
        string path,
        bool required)
    {
        if (value is null)
        {
            if (required)
            {
                Fail($"{path} is required");
            }

            return;
        }

        try
        {
            switch (type)
            {
                case PointValueType.Analog:
                    var analog = ReadNumber(value, path);
                    if (!double.IsFinite(analog))
                    {
                        Fail($"{path} must be finite");
                    }
                    ValidateRange(analog, limits, path);
                    break;
                case PointValueType.Integer:
                    var integer = ReadNumber(value, path);
                    ValidateSafeWholeNumber(integer, path);
                    ValidateRange(integer, limits, path);
                    break;
                case PointValueType.Digital:
                    _ = value.GetValue<bool>();
                    break;
                case PointValueType.MultiState:
                    var key = value.GetValue<string>();
                    if (states?.Any(state => state.Key == key) != true)
                    {
                        Fail($"{path} must match a state key");
                    }
                    break;
                case PointValueType.Text:
                    var text = value.GetValue<string>();
                    if (text.Length > limits!.MaximumLength)
                    {
                        Fail($"{path} exceeds limits.maximumLength");
                    }
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            Fail($"{path} does not match valueType");
        }
    }

    private static PointSafetyPolicy? ParseSafetyPolicy(
        JsonObject? value,
        bool commandable)
    {
        if (!commandable)
        {
            if (value is not null)
            {
                Fail("safeDisablePolicy applies only to commandable points");
            }
            return null;
        }

        if (value is null)
        {
            Fail("commandable points require safeDisablePolicy");
        }

        return new PointSafetyPolicy(
            RequiredPolicy(value!, "startup"),
            RequiredPolicy(value!, "shutdown"),
            RequiredPolicy(value!, "communicationLoss"),
            RequiredPolicy(value!, "disable"));
    }

    private static string RequiredPolicy(JsonObject value, string key)
    {
        var policy = RequiredString(value, key);
        if (policy is not "hold_last" and not "safe_value"
            and not "relinquish" and not "stop_driving")
        {
            Fail($"safeDisablePolicy.{key} is invalid");
        }
        return policy;
    }

    private static void ValidateUnits(string? units, PointValueType type)
    {
        if (units is null)
        {
            return;
        }

        if (!PointCompatibility.SupportsUnits(type))
        {
            Fail("units apply only to analog and integer points");
        }

        if (units != units.Trim() || !UnitRegex().IsMatch(units))
        {
            Fail("units must be a normalized identifier");
        }
    }

    private static void RejectCredentialLiterals(JsonNode node, string path)
    {
        if (node is JsonObject mapping)
        {
            foreach (var item in mapping)
            {
                if (item.Key.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || item.Key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                    || item.Key.Contains("token", StringComparison.OrdinalIgnoreCase)
                    || item.Key.Equals("authorization", StringComparison.OrdinalIgnoreCase))
                {
                    Fail($"{path} cannot contain credential literals");
                }
                if (item.Value is not null)
                {
                    RejectCredentialLiterals(item.Value, $"{path}.{item.Key}");
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array.Where(item => item is not null))
            {
                RejectCredentialLiterals(item!, path);
            }
        }
    }

    private static void RejectDuplicates(IEnumerable<string> values, string description)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (!seen.Add(value))
            {
                Fail($"duplicate {description} \"{value}\"");
            }
        }
    }

    private static double? OptionalFiniteNumber(JsonObject value, string key)
    {
        if (value[key] is null)
        {
            return null;
        }

        var result = ReadNumber(value[key]!, key);
        if (!double.IsFinite(result))
        {
            Fail($"{key} must be finite");
        }

        return result;
    }

    private static double ReadNumber(JsonNode value, string path)
    {
        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue;
            }
            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }
            if (jsonValue.TryGetValue<decimal>(out var decimalValue))
            {
                return (double)decimalValue;
            }
        }

        if (!double.TryParse(
            SafeJson(value),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result))
        {
            Fail($"{path} must be a number");
        }

        return result;
    }

    private static string SafeJson(JsonNode value)
    {
        try
        {
            return value.ToJsonString();
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static int? OptionalInteger(JsonObject value, string key)
    {
        if (value[key] is null)
        {
            return null;
        }

        try
        {
            return value[key]!.GetValue<int>();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or FormatException or OverflowException)
        {
            Fail($"{key} must be an integer");
            return null;
        }
    }

    private static bool? OptionalBoolean(JsonObject value, string key)
    {
        if (value[key] is null)
        {
            return null;
        }

        try { return value[key]!.GetValue<bool>(); }
        catch (InvalidOperationException)
        {
            Fail($"{key} must be a boolean");
            return null;
        }
    }

    private static string RequiredString(JsonObject value, string key) =>
        OptionalString(value, key)
        ?? throw new PointDefinitionValidationException($"{key} is required");

    private static string? OptionalString(JsonObject value, string key)
    {
        if (value[key] is null)
        {
            return null;
        }

        try
        {
            var result = value[key]!.GetValue<string>();
            if (string.IsNullOrWhiteSpace(result) || result != result.Trim())
            {
                Fail($"{key} must be non-empty without surrounding whitespace");
            }
            return result;
        }
        catch (InvalidOperationException)
        {
            Fail($"{key} must be a string");
            return null;
        }
    }

    private static void ValidateSafeWholeNumber(double? value, string path)
    {
        if (value is not null
            && (value != Math.Truncate(value.Value) || Math.Abs(value.Value) > MaximumSafeInteger))
        {
            Fail($"{path} must be a safe whole JSON number");
        }
    }

    private static void ValidateRange(double value, PointLimits? limits, string path)
    {
        if (value < limits?.Minimum || value > limits?.Maximum)
        {
            Fail($"{path} is outside configured limits");
        }
    }

    private static PointImplementation ParseImplementation(string value) => value switch
    {
        "virtual" => PointImplementation.Virtual,
        "bound" => PointImplementation.Bound,
        _ => throw new PointDefinitionValidationException("implementation is invalid"),
    };

    private static PointDirection ParseDirection(string value) => value switch
    {
        "input" => PointDirection.Input,
        "output" => PointDirection.Output,
        "input_output" => PointDirection.InputOutput,
        "value" => PointDirection.Value,
        _ => throw new PointDefinitionValidationException("direction is invalid"),
    };

    private static PointValueType ParseValueType(string value) => value switch
    {
        "analog" => PointValueType.Analog,
        "digital" => PointValueType.Digital,
        "multi_state" => PointValueType.MultiState,
        "integer" => PointValueType.Integer,
        "text" => PointValueType.Text,
        _ => throw new PointDefinitionValidationException("valueType is invalid"),
    };

    private static PointPersistence ParsePersistence(string value) => value switch
    {
        "volatile" => PointPersistence.Volatile,
        "retained" => PointPersistence.Retained,
        _ => throw new PointDefinitionValidationException("persistence is invalid"),
    };

    private static PointSourceKind ParseSourceKind(string value) => value switch
    {
        "home_assistant" => PointSourceKind.HomeAssistant,
        "mqtt" => PointSourceKind.Mqtt,
        "http_json" => PointSourceKind.HttpJson,
        _ => throw new PointDefinitionValidationException("source kind is invalid"),
    };

    private static void Fail(string message) =>
        throw new PointDefinitionValidationException(message);

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.%/-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex UnitRegex();
}