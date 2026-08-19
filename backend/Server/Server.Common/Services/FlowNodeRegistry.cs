using Server.Common.Contracts;

namespace Server.Common.Services;

public static class FlowNodeRegistry
{
    public static IReadOnlySet<FlowFunctionKind> Functions { get; } = Enum.GetValues<FlowFunctionKind>().ToHashSet();
}