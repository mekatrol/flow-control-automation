using Server.Common.Models;
using Server.Common.Types;
using System.Text.Json;

namespace Server.Common.Services;

public static class VirtualPointNodes
{
    public static bool IsVirtual(this FlowNodeType kind) =>
        kind is FlowNodeType.AnalogVirtual or FlowNodeType.DigitalVirtual;

    public static FlowNodeType ExecutableKind(this FlowNodeType kind) => kind switch
    {
        FlowNodeType.AnalogVirtual => FlowNodeType.AnalogInput,
        FlowNodeType.DigitalVirtual => FlowNodeType.DigitalInput,
        _ => kind
    };

    public static IReadOnlyList<VirtualPointDeclaration> Declarations(
        IEnumerable<FlowNode> nodes) =>
        [.. nodes.Where(node => node.Kind.IsVirtual()).Select(Declaration)];

    private static VirtualPointDeclaration Declaration(FlowNode node)
    {
        var analog = node.Kind == FlowNodeType.AnalogVirtual;
        var persistence = Text(node, "persistence") == "retained"
            ? VirtualPointPersistenceType.Retained
            : VirtualPointPersistenceType.Volatile;
        var units = analog ? Text(node, "units") : null;
        return new VirtualPointDeclaration
        {
            Key = Text(node, "pointId") ?? string.Empty,
            ValueType = analog ? AutomationPointValueType.Analog : AutomationPointValueType.Digital,
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