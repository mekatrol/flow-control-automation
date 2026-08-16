using Server.Services.Contracts;

namespace Server.Services.Implementation;

public static class PointCompatibility
{
    public static bool CanRead(DataDirection direction) =>
        direction is DataDirection.Input
            or DataDirection.Output
            or DataDirection.InputOutput
            or DataDirection.Value;

    public static bool CanCommand(DataDirection direction) =>
        direction is DataDirection.Output
            or DataDirection.InputOutput
            or DataDirection.Value;

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