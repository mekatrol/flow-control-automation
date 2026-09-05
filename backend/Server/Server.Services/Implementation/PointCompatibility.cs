using Server.Common.Contracts;
using Server.Common.Models;

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