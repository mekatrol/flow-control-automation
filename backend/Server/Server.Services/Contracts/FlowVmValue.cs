using Server.Services.Extensions;

namespace Server.Services.Contracts;

public sealed record FlowVmValue
{
    public required string Type { get; init; }

    public bool Boolean { get; init; }

    public double Number { get; init; }

    public string Quality { get; init; } = DataQualityExtensions.Good;

    public static FlowVmValue FromBoolean(bool value, string quality = DataQualityExtensions.Good) =>
        new() { Type = DataType.Boolean.ToFriendlyString(), Boolean = value, Quality = quality };

    public static FlowVmValue FromNumber(double value, string quality = DataQualityExtensions.Good)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return new() { Type = DataType.Number.ToFriendlyString(), Number = value, Quality = quality };
    }

    public static implicit operator FlowVmValue(bool value) => FromBoolean(value);
}