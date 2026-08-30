using System.Text.Json;

namespace Server.Common.Contracts;

public static class VirtualPointNodes
{
    public static bool IsVirtual(this FlowNodeKind kind) =>
        kind is FlowNodeKind.AnalogVirtual or FlowNodeKind.DigitalVirtual;

    public static FlowNodeKind ExecutableKind(this FlowNodeKind kind) => kind switch
    {
        FlowNodeKind.AnalogVirtual => FlowNodeKind.AnalogInput,
        FlowNodeKind.DigitalVirtual => FlowNodeKind.DigitalInput,
        _ => kind
    };

    public static IReadOnlyList<VirtualPointDeclaration> Declarations(
        IEnumerable<FlowNode> nodes) =>
        [.. nodes.Where(node => node.Kind.IsVirtual()).Select(Declaration)];

    private static VirtualPointDeclaration Declaration(FlowNode node)
    {
        var analog = node.Kind == FlowNodeKind.AnalogVirtual;
        var persistence = Text(node, "persistence") == "retained"
            ? VirtualPointPersistence.Retained
            : VirtualPointPersistence.Volatile;
        var units = analog ? Text(node, "units") : null;
        return new VirtualPointDeclaration
        {
            Key = Text(node, "pointId") ?? string.Empty,
            ValueType = analog ? FlowPointValueType.Analog : FlowPointValueType.Digital,
            Units = string.IsNullOrWhiteSpace(units) ? null : units,
            Readable = true,
            Commandable = true,
            Persistence = persistence,
            RelinquishDefault = node.Configuration.TryGetValue("relinquishDefault", out var value)
                && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                    ? value
                    : null
        };
    }

    private static string? Text(FlowNode node, string key) =>
        node.Configuration.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}