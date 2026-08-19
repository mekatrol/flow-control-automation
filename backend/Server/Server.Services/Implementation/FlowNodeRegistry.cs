using Server.Common.Contracts;

namespace Server.Services.Implementation;

public static class FlowNodeRegistry
{
    public static IReadOnlySet<FlowFunctionKind> Functions { get; } = Enum.GetValues<FlowFunctionKind>().ToHashSet();
}