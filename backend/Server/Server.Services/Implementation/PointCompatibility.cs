using Server.Common.Types;

namespace Server.Services.Implementation;

public static class PointCompatibility
{
    public static bool CanRead(DataDirectionType direction) =>
        direction is DataDirectionType.Input
            or DataDirectionType.Output
            or DataDirectionType.InputOutput
            or DataDirectionType.Value;

    public static bool CanCommand(DataDirectionType direction) =>
        direction is DataDirectionType.Output
            or DataDirectionType.InputOutput
            or DataDirectionType.Value;

    public static bool SupportsUnits(FlowPointValueType valueType) =>
        valueType is FlowPointValueType.Analog or FlowPointValueType.Integer;

    public static bool ValuesAreCompatible(
        FlowPointValueType source,
        string? sourceUnits,
        FlowPointValueType target,
        string? targetUnits) =>
        source == target
        && (!SupportsUnits(source)
            || string.Equals(sourceUnits, targetUnits, StringComparison.Ordinal));
}