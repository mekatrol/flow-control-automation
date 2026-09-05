using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record FlowVmValue
{
    public required DataType DataType { get; init; }

    public bool Boolean { get; init; }

    public double Number { get; init; }

    public DataQualityType Quality { get; init; } = DataQualityType.Good;

    public static FlowVmValue FromBoolean(bool value, DataQualityType quality = DataQualityType.Good) =>
        new() { DataType = DataType.Boolean, Boolean = value, Quality = quality };

    public static FlowVmValue FromNumber(double value, DataQualityType quality = DataQualityType.Good)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return new() { DataType = DataType.Number, Number = value, Quality = quality };
    }

    public static implicit operator FlowVmValue(bool value) => FromBoolean(value);
}