using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

#pragma warning disable CC0001, CC0002, IDE0007, IDE0011

namespace Server.Services.Points.Runtime;

internal sealed class PointValueConverter : IPointValueConverter
{
    private const long MaximumSafeInteger = 9_007_199_254_740_991;

    public PointValueConversionResult Parse(AutomationPoint point, string? rawValue)
    {
        if (rawValue is null)
        {
            return Bad("Mapping alias returned null.");
        }

        try
        {
            return point.ValueType switch
            {
                AutomationPointValueType.Analog => Number(point, double.Parse(rawValue.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)),
                AutomationPointValueType.Integer => Integer(point, long.Parse(rawValue.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)),
                AutomationPointValueType.Digital when rawValue.Trim() == "true" => Good(JsonValue.Create(true)),
                AutomationPointValueType.Digital when rawValue.Trim() == "false" => Good(JsonValue.Create(false)),
                AutomationPointValueType.Digital => Bad("Digital value must be exactly 'true' or 'false'."),
                AutomationPointValueType.MultiState => MultiState(point, rawValue.Trim()),
                AutomationPointValueType.Text => Text(point, rawValue),
                _ => Bad("Point value type is unsupported.")
            };
        }
        catch (FormatException)
        {
            return Bad($"Value is not valid {point.ValueType.ToString().ToLowerInvariant()} transport text.");
        }
        catch (OverflowException)
        {
            return Bad("Numeric value is outside the supported range.");
        }
    }

    public string Serialize(AutomationPoint point, object? value)
    {
        JsonNode? node = value switch
        {
            JsonNode json => json,
            null => null,
            _ => JsonSerializer.SerializeToNode(value, FlowControlJson.Options)
        };
        var raw = point.ValueType switch
        {
            AutomationPointValueType.Analog when node is JsonValue n && n.TryGetValue<double>(out var number) => number.ToString("R", CultureInfo.InvariantCulture),
            AutomationPointValueType.Integer when node is JsonValue n && n.TryGetValue<long>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
            AutomationPointValueType.Digital when node is JsonValue n && n.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
            AutomationPointValueType.MultiState or AutomationPointValueType.Text when node is JsonValue n && n.TryGetValue<string>(out var text) => text,
            _ => throw new ArgumentException("Command value does not match the point value type.", nameof(value))
        };

        var checkedValue = Parse(point, raw);
        if (checkedValue.Quality != DataQualityType.Good)
        {
            throw new ArgumentException(checkedValue.Diagnostic, nameof(value));
        }
        return raw;
    }

    private static PointValueConversionResult Number(AutomationPoint point, double value)
    {
        if (!double.IsFinite(value)) return Bad("Analog value must be finite.");
        var limits = point.Limits;
        if (limits?["minimum"]?.GetValue<double>() is double minimum && value < minimum
            || limits?["maximum"]?.GetValue<double>() is double maximum && value > maximum)
            return Bad("Analog value is outside configured limits.");
        return Good(JsonValue.Create(value));
    }

    private static PointValueConversionResult Integer(AutomationPoint point, long value)
    {
        if (value is < -MaximumSafeInteger or > MaximumSafeInteger) return Bad("Integer exceeds the JSON safe-integer range.");
        var limits = point.Limits;
        if (limits?["minimum"]?.GetValue<long>() is long minimum && value < minimum
            || limits?["maximum"]?.GetValue<long>() is long maximum && value > maximum)
            return Bad("Integer value is outside configured limits.");
        return Good(JsonValue.Create(value));
    }

    private static PointValueConversionResult MultiState(AutomationPoint point, string value)
    {
        var valid = point.StateLabels is JsonArray labels && labels.OfType<JsonObject>()
            .Any(label => label["key"]?.GetValue<string>() == value);
        return valid ? Good(JsonValue.Create(value)) : Bad("Multi-state value is not a configured state key.");
    }

    private static PointValueConversionResult Text(AutomationPoint point, string value)
    {
        var maximum = point.Limits?["maximumLength"]?.GetValue<int>();
        return maximum is not null && value.Length > maximum
            ? Bad("Text value exceeds maximumLength.")
            : Good(JsonValue.Create(value));
    }

    private static PointValueConversionResult Good(JsonNode? value) => new(value, DataQualityType.Good);
    private static PointValueConversionResult Bad(string diagnostic) => new(null, DataQualityType.Bad, diagnostic);
}