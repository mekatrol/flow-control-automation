using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Common.Types;

namespace Server.Common.Models;

/// <summary>Serializes automation points using their required source type.</summary>
public sealed class AutomationPointJsonConverter : JsonConverter<AutomationPoint>
{
    public override AutomationPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var sourceName = options.PropertyNamingPolicy?.ConvertName(nameof(AutomationPoint.PointSourceType))
            ?? nameof(AutomationPoint.PointSourceType);
        if (!root.TryGetProperty(sourceName, out var source))
        {
            throw new JsonException("pointSourceType is required");
        }
        var sourceType = source.Deserialize<PointSourceType>(options);

        return sourceType switch
        {
            PointSourceType.Virtual => root.Deserialize<VirtualAutomationPoint>(options)!,
            PointSourceType.Physical => root.Deserialize<PhysicalAutomationPoint>(options)!,
            PointSourceType.Remote => root.Deserialize<RemoteAutomationPoint>(options)!,
            _ => throw new JsonException("pointSourceType is invalid")
        };
    }

    public override void Write(Utf8JsonWriter writer, AutomationPoint value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
