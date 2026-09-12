using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Server.Services.Points.Validation;

internal sealed partial class PointDefinitionValidator : IPointDefinitionValidator
{
    private const long MaximumSafeInteger = 9_007_199_254_740_991;

    public ValidatedPointDefinition Validate(
        AutomationPoint point,
        PointValidationContext context)
    {
        ValidateIdentity(point.Id, point.Name, "point");

        var owner = context.Sources.Values.SingleOrDefault(source =>
            source.Points.Any(candidate => candidate.Id == point.Id));
        var isVirtual = owner?.Kind == PointSourceKind.Virtual || point.Direction == DataDirectionType.Value;
        var direction = point.Direction;
        var valueType = point.ValueType;
        var persistence = ParsePersistence(point.Persistence);

        ValidateCapabilities(point, isVirtual, direction);

        var limits = ParseLimits(point.Limits, valueType);
        var (digitalLabels, multiStateLabels) = ParseLabels(point.StateLabels, valueType);

        ValidateUnits(point.Units, valueType);
        ValidateValue(point.RelinquishDefault, valueType, limits, multiStateLabels,
            "relinquishDefault", required: isVirtual &&
            persistence == PointPersistence.Retained);

        var (sourceKind, mapping) = ValidateBinding(point, owner);

        var safety = ParseSafetyPolicy(
            point.SafeDisablePolicy,
            point.Commandable && !isVirtual);

        return new ValidatedPointDefinition(
            point, direction, valueType, persistence, limits,
            digitalLabels, multiStateLabels, safety, sourceKind, mapping);
    }

    public void ValidateDocument(
        PointDocument document,
        IReadOnlyDictionary<string, PointSource> sources)
    {
        if (document.SchemaVersion != 1)
        {
            Fail("schemaVersion must be 1");
        }

        RejectDuplicates(document.Points.Select(point => point.Id), "point id");
        RejectDuplicates(document.Points.Select(point => point.Name), "point name");

        var context = new PointValidationContext(sources);

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
        AutomationPoint point,
        bool isVirtual,
        DataDirectionType direction)
    {
        if (isVirtual && direction != DataDirectionType.Value)
        {
            Fail("virtual points must use value direction");
        }

        if (!isVirtual && direction == DataDirectionType.Value)
        {
            Fail("physical and remote points cannot use value direction");
        }

        if (point.Commandable && !PointCompatibility.CanCommand(direction))
        {
            Fail($"{point.Direction} points cannot be commandable");
        }

        if (direction == DataDirectionType.Input && (!point.Readable || point.Commandable))
        {
            Fail("input points must be readable and not commandable");
        }

        if (direction == DataDirectionType.Output && !point.Commandable)
        {
            Fail("output points must be commandable");
        }

        if (direction == DataDirectionType.InputOutput && (!point.Readable || !point.Commandable))
        {
            Fail("input_output points must be readable and commandable");
        }

        if (direction == DataDirectionType.Value && !point.Readable && !point.Commandable)
        {
            Fail("value points must be readable or commandable");
        }
    }

    private static (PointSourceKind? Kind, PointMapping? Mapping) ValidateBinding(
        AutomationPoint point,
        PointSource? source)
    {
        if (source is null)
        {
            return (null, null);
        }

        var segments = point.Mapping.Split('/', StringSplitOptions.None);
        var mapping = segments.Length == 2
            ? source.Mappings.SingleOrDefault(candidate => candidate.Id == segments[0])
            : null;

        return mapping is null
            ? throw new PointDefinitionValidationException("mapping reference is invalid")
            : (source.Kind, mapping);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051", Justification = "Removed in aggregate persistence phase")]
    private static PointMapping ParseMapping(
        AutomationPoint point,
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
            _ => throw new InvalidOperationException("Unsupported source kind.")
        };

    private static MqttPointMapping ParseMqttMapping(AutomationPoint point, JsonObject mapping)
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

    private static HttpJsonPointMapping ParseHttpMapping(AutomationPoint point, JsonObject mapping)
    {
        var path = RequiredString(mapping, "path");

        if (!path.StartsWith('/')
            || path.StartsWith("//", StringComparison.Ordinal)
            || path.Contains("://", StringComparison.Ordinal))
        {
            Fail("mapping.path must be a relative absolute-path starting with /");
        }

        var method = (OptionalString(mapping, "method") ?? "GET").ToUpperInvariant();

        if (point.Readable && !point.Commandable && method is not "GET" and not "HEAD")
        {
            Fail("readable HTTP mappings must use GET or HEAD");
        }

        if (point.Commandable && method is not "POST" and not "PUT" and not "PATCH")
        {
            Fail("commandable HTTP mappings must use POST, PUT, or PATCH");
        }

        var jsonPointer = OptionalString(mapping, "jsonPointer");
        var valuePointer = OptionalString(mapping, "valuePointer");
        var contentType = OptionalString(mapping, "contentType") ?? "application/json";

        if (jsonPointer is not null && !jsonPointer.StartsWith('/'))
        {
            Fail("mapping.jsonPointer must be a JSON Pointer starting with /");
        }

        if (valuePointer is not null && !valuePointer.StartsWith('/'))
        {
            Fail("mapping.valuePointer must be a JSON Pointer starting with /");
        }

        if (contentType is not "application/json" and not "application/x-www-form-urlencoded")
        {
            Fail("mapping.contentType must be application/json or application/x-www-form-urlencoded");
        }

        if (contentType == "application/x-www-form-urlencoded"
            && (valuePointer is null || valuePointer[1..].Contains('/')))
        {
            Fail("form-encoded HTTP mappings require a single-segment mapping.valuePointer");
        }

        return new HttpJsonPointMapping(
            path,
            method,
            jsonPointer,
            valuePointer,
            contentType);
    }

    private static PointLimits? ParseLimits(JsonObject? value, AutomationPointValueType type)
    {
        if (value is null)
        {
            if (type == AutomationPointValueType.Text)
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

        if (type == AutomationPointValueType.Integer)
        {
            ValidateSafeWholeNumber(minimum, "limits.minimum");
            ValidateSafeWholeNumber(maximum, "limits.maximum");
        }

        if (type is not AutomationPointValueType.Analog and not AutomationPointValueType.Integer
            && (minimum is not null || maximum is not null))
        {
            Fail("limits.minimum and limits.maximum apply only to numeric points");
        }

        if (type == AutomationPointValueType.Text && maximumLength is not > 0)
        {
            Fail("text points require a positive limits.maximumLength");
        }

        if (type != AutomationPointValueType.Text && maximumLength is not null)
        {
            Fail("limits.maximumLength applies only to text points");
        }

        return new PointLimits(minimum, maximum, maximumLength);
    }

    private static (DigitalStateLabels?, IReadOnlyList<MultiStateLabel>?) ParseLabels(
        JsonNode? value,
        AutomationPointValueType type)
    {
        if (type == AutomationPointValueType.Digital)
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

        if (type == AutomationPointValueType.MultiState)
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
        AutomationPointValueType type,
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
                case AutomationPointValueType.Analog:
                    var analog = ReadNumber(value, path);

                    if (!double.IsFinite(analog))
                    {
                        Fail($"{path} must be finite");
                    }

                    ValidateRange(analog, limits, path);
                    break;
                case AutomationPointValueType.Integer:
                    var integer = ReadNumber(value, path);
                    ValidateSafeWholeNumber(integer, path);
                    ValidateRange(integer, limits, path);
                    break;
                case AutomationPointValueType.Digital:
                    _ = value.GetValue<bool>();
                    break;
                case AutomationPointValueType.MultiState:
                    var key = value.GetValue<string>();

                    if (states?.Any(state => state.Key == key) != true)
                    {
                        Fail($"{path} must match a state key");
                    }

                    break;
                case AutomationPointValueType.Text:
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

    private static void ValidateUnits(string? units, AutomationPointValueType type)
    {
        if (units is null)
        {
            return;
        }

        if (!PointCompatibility.SupportsUnits(type))
        {
            Fail("units apply only to analog and integer points");
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

    private static PointPersistence ParsePersistence(string value) => value switch
    {
        "volatile" => PointPersistence.Volatile,
        "retained" => PointPersistence.Retained,
        _ => throw new PointDefinitionValidationException("persistence is invalid")
    };

    private static void Fail(string message) =>
        throw new PointDefinitionValidationException(message);

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

}