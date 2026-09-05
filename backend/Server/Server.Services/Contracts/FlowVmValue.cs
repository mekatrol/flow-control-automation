using Server.Common.Models;

namespace Server.Services.Contracts;

public sealed record FlowVmValue
{
    public required DataType DataType { get; init; }

    public bool Boolean { get; init; }

    public double Number { get; init; }

    public DataQuality Quality { get; init; } = DataQuality.Good;

    public static FlowVmValue FromBoolean(bool value, DataQuality quality = DataQuality.Good) =>
        new() { DataType = DataType.Boolean, Boolean = value, Quality = quality };

    public static FlowVmValue FromNumber(double value, DataQuality quality = DataQuality.Good)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return new() { DataType = DataType.Number, Number = value, Quality = quality };
    }

    public static implicit operator FlowVmValue(bool value) => FromBoolean(value);
}