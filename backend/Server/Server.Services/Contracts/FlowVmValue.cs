namespace Server.Services.Contracts;

public sealed record FlowVmValue
{
    public required string Type { get; init; }
    public bool Boolean { get; init; }
    public double Number { get; init; }
    public string Quality { get; init; } = "good";

    public static FlowVmValue FromBoolean(bool value, string quality = "good") =>
        new() { Type = "boolean", Boolean = value, Quality = quality };

    public static FlowVmValue FromNumber(double value, string quality = "good")
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return new() { Type = "number", Number = value, Quality = quality };
    }

    public static implicit operator FlowVmValue(bool value) => FromBoolean(value);
}