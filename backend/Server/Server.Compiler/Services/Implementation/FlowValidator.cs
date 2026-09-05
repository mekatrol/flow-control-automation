using Server.Common.Models;
using Server.Common.Services;
using Server.Common.Types;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Compiler.Services.Implementation;

internal partial class FlowValidator : IFlowValidator
{
    private static readonly HashSet<FlowNodeType> ValidKinds =
    [
        FlowNodeType.A2D, FlowNodeType.Add, FlowNodeType.Subtract, FlowNodeType.Multiply, FlowNodeType.Divide, FlowNodeType.Power, FlowNodeType.Negate, FlowNodeType.AnalogInput, FlowNodeType.AnalogOutput, FlowNodeType.And, FlowNodeType.Average, FlowNodeType.Calculator, FlowNodeType.Calendar, FlowNodeType.Clamp, FlowNodeType.Comparator,
        FlowNodeType.Delay, FlowNodeType.DigitalConstant, FlowNodeType.DigitalInput, FlowNodeType.DigitalOutput, FlowNodeType.DigitalSwitch,
        FlowNodeType.LevelShifter, FlowNodeType.Line, FlowNodeType.Max, FlowNodeType.Memory, FlowNodeType.Min, FlowNodeType.Nand, FlowNodeType.Nor, FlowNodeType.Not, FlowNodeType.AnalogConstant, FlowNodeType.Or, FlowNodeType.Override,
        FlowNodeType.Pulse, FlowNodeType.Schedule, FlowNodeType.AnalogSwitch, FlowNodeType.Sequence, FlowNodeType.Split,
        FlowNodeType.D2A, FlowNodeType.OnDelay, FlowNodeType.QualityGood, FlowNodeType.RisingEdge, FlowNodeType.Timer, FlowNodeType.Xnor, FlowNodeType.Xor, FlowNodeType.Counter, FlowNodeType.Clock, FlowNodeType.AnalogVirtual, FlowNodeType.DigitalVirtual,
    ];

    private static readonly HashSet<string> ValidStatuses = ["draft", "deployed"];

    private static readonly HashSet<DataDirectionType> ValidDirections = [DataDirectionType.Input, DataDirectionType.Output];

    private static readonly HashSet<DataType> ValidDataTypes =
        [DataType.Any, DataType.Boolean, DataType.Event, DataType.Number, DataType.String];

    private static readonly HashSet<string> ValidSides = ["left", "right", "top", "bottom"];

    public void Validate(Flow flow)
    {
        if (string.IsNullOrWhiteSpace(flow.Id) || string.IsNullOrWhiteSpace(flow.Name))
        {
            throw new FlowValidationException("id and name must be non-empty");
        }

        if (!ValidStatuses.Contains(flow.Status))
        {
            throw new FlowValidationException($"unsupported flow status \"{flow.Status}\"");
        }

        ValidateVirtualPoints(
            VirtualPointNodes.Declarations(flow.Nodes),
            allowUnmapped: flow.Status == "draft");

        if (flow.Revision < 1)
        {
            throw new FlowValidationException("revision must be positive");
        }

        if (!Rfc3339Regex().IsMatch(flow.UpdatedAt)
            || !DateTimeOffset.TryParse(
                flow.UpdatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new FlowValidationException("updatedAt must be an RFC 3339 date-time");
        }

        var nodes = new Dictionary<string, Dictionary<string, FlowConnector>>();
        for (var nodeIndex = 0; nodeIndex < flow.Nodes.Count; nodeIndex++)
        {
            var node = flow.Nodes[nodeIndex];
            if (string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Label))
            {
                throw new FlowValidationException(
                    $"nodes[{nodeIndex}]: id and label must be non-empty");
            }

            if (nodes.ContainsKey(node.Id))
            {
                throw new FlowValidationException($"nodes: duplicate id \"{node.Id}\"");
            }

            if (!ValidKinds.Contains(node.Kind))
            {
                throw new FlowValidationException($"nodes[{nodeIndex}]: unsupported kind");
            }

            if (!double.IsFinite(node.X)
                || !double.IsFinite(node.Y)
                || !double.IsFinite(node.ZOrder))
            {
                throw new FlowValidationException(
                    $"nodes[{nodeIndex}]: coordinates and zOrder must be finite");
            }

            var connectors = new Dictionary<string, FlowConnector>();
            for (var connectorIndex = 0; connectorIndex < node.Connectors.Count; connectorIndex++)
            {
                var connector = node.Connectors[connectorIndex];
                if (string.IsNullOrWhiteSpace(connector.Id)
                    || string.IsNullOrWhiteSpace(connector.Label)
                    || !ValidDirections.Contains(connector.Direction)
                    || !ValidDataTypes.Contains(connector.DataType)
                    || !ValidSides.Contains(connector.Side))
                {
                    throw new FlowValidationException(
                        $"nodes[{nodeIndex}].connectors[{connectorIndex}]: invalid connector");
                }

                if (!connectors.TryAdd(connector.Id, connector))
                {
                    throw new FlowValidationException(
                        $"nodes[{nodeIndex}].connectors: duplicate id \"{connector.Id}\"");
                }
            }

            foreach (var (key, value) in node.Configuration)
            {
                if (!IsFiniteScalar(value))
                {
                    var detail = value.ValueKind == JsonValueKind.Number
                        ? "number must be finite"
                        : "value must be a JSON scalar";
                    throw new FlowValidationException(
                        $"nodes[{nodeIndex}].configuration.{key}: {detail}");
                }
            }

            nodes.Add(node.Id, connectors);
        }

        var connectionIds = new HashSet<string>();

        for (var index = 0; index < flow.Connections.Count; index++)
        {
            var connection = flow.Connections[index];

            if (string.IsNullOrWhiteSpace(connection.Id) || !connectionIds.Add(connection.Id))
            {
                throw new FlowValidationException($"connections[{index}]: id must be non-empty and unique");
            }

            if (!TryGetConnector(nodes, connection.Start, out var start) ||
                !TryGetConnector(nodes, connection.End, out var end))
            {
                throw new FlowValidationException($"connections[{index}]: endpoint does not exist");
            }

            if (start.Direction != DataDirectionType.Output || end.Direction != DataDirectionType.Input)
            {
                throw new FlowValidationException(
                    $"connections[{index}]: connection must run from output to input");
            }

            if (start.DataType != DataType.Any &&
                end.DataType != DataType.Any &&
                start.DataType != end.DataType)
            {
                throw new FlowValidationException(
                    $"connections[{index}]: connector data types are incompatible");
            }
        }
    }

    private static void ValidateVirtualPoints(
        IReadOnlyList<VirtualPointDeclaration> declarations,
        bool allowUnmapped)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (item, index) in declarations.Select((item, index) => (item, index)))
        {
            if (allowUnmapped &&
                (string.IsNullOrWhiteSpace(item.Key) || keys.Contains(item.Key)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Key) || !keys.Add(item.Key))
            {
                throw new FlowValidationException($"virtualPointDeclarations[{index}].key must be non-empty and unique");
            }

            if (item.ValueType is not (FlowPointValueType.Analog or FlowPointValueType.Digital))
            {
                throw new FlowValidationException($"virtualPointDeclarations[{index}].valueType must be analog or digital");
            }

            if (!item.Readable && !item.Commandable)
            {
                throw new FlowValidationException($"virtualPointDeclarations[{index}] must be readable or commandable");
            }

            if (item.ValueType == FlowPointValueType.Digital && item.Units is not null)
            {
                throw new FlowValidationException($"virtualPointDeclarations[{index}].units are only valid for analog points");
            }

            if (item.RelinquishDefault is { } value &&
                (item.ValueType == FlowPointValueType.Analog
                    ? value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number)
                    : value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
            {
                throw new FlowValidationException($"virtualPointDeclarations[{index}].relinquishDefault does not match valueType");
            }
        }
    }

    private static bool IsFiniteScalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False or JsonValueKind.String
            => true,
        JsonValueKind.Number => value.TryGetDouble(out var number) && double.IsFinite(number),
        _ => false,
    };

    private static bool TryGetConnector(
        Dictionary<string, Dictionary<string, FlowConnector>> nodes,
        FlowEndpoint endpoint,
        out FlowConnector connector)
    {
        if (nodes.TryGetValue(endpoint.NodeId, out var connectors) &&
            connectors.TryGetValue(endpoint.ConnectorId, out var found))
        {
            connector = found;
            return true;
        }

        connector = null!;
        return false;
    }

    [GeneratedRegex(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Rfc3339Regex();
}
