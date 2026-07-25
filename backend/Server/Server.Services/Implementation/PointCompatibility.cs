using Server.Services.Contracts;

namespace Server.Services.Implementation;

public static class PointCompatibility
{
    public static bool CanRead(PointDirection direction) =>
        direction is PointDirection.Input
            or PointDirection.Output
            or PointDirection.InputOutput
            or PointDirection.Value;

    public static bool CanCommand(PointDirection direction) =>
        direction is PointDirection.Output
            or PointDirection.InputOutput
            or PointDirection.Value;

    public static bool SupportsUnits(PointValueType valueType) =>
        valueType is PointValueType.Analog or PointValueType.Integer;

    public static bool ValuesAreCompatible(
        PointValueType source,
        string? sourceUnits,
        PointValueType target,
        string? targetUnits) =>
        source == target
        && (!SupportsUnits(source)
            || string.Equals(sourceUnits, targetUnits, StringComparison.Ordinal));
}