using Server.Common.Types;

namespace Server.Common.Services;

public static class FlowNodeRegistry
{
    public static IReadOnlySet<FlowFunctionType> Functions { get; } = Enum.GetValues<FlowFunctionType>().ToHashSet();
}