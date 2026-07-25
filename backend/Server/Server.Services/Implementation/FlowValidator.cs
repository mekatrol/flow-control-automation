using Server.Services.Contracts;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

internal static partial class FlowValidator
{
    private static readonly HashSet<string> ValidKinds =
    [
        "and", "average", "calculator", "calendar", "clamp", "comparator",
        "delay", "if", "invert", "line", "max", "min", "nand", "nor", "not",
        "or", "override", "pulse", "schedule", "selector", "sequence", "split",
        "timer", "xnor", "xor",
    ];

    private static readonly HashSet<string> ValidStatuses = ["draft", "deployed"];
    private static readonly HashSet<string> ValidDirections = ["input", "output"];
    private static readonly HashSet<string> ValidDataTypes =
        ["any", "boolean", "event", "number", "string"];
    private static readonly HashSet<string> ValidSides = ["left", "right", "top", "bottom"];

    public static void Validate(Flow flow)
    {
        if (string.IsNullOrWhiteSpace(flow.Id) || string.IsNullOrWhiteSpace(flow.Name))
        {
            throw new FlowValidationException("id and name must be non-empty");
        }

        if (!ValidStatuses.Contains(flow.Status))
        {
            throw new FlowValidationException($"unsupported flow status \"{flow.Status}\"");
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
                throw new FlowValidationException(
                    $"connections[{index}]: id must be non-empty and unique");
            }

            if (!TryGetConnector(nodes, connection.Start, out var start)
                || !TryGetConnector(nodes, connection.End, out var end))
            {
                throw new FlowValidationException($"connections[{index}]: endpoint does not exist");
            }

            if (start.Direction != "output" || end.Direction != "input")
            {
                throw new FlowValidationException(
                    $"connections[{index}]: connection must run from output to input");
            }

            if (start.DataType != "any"
                && end.DataType != "any"
                && start.DataType != end.DataType)
            {
                throw new FlowValidationException(
                    $"connections[{index}]: connector data types are incompatible");
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
        IReadOnlyDictionary<string, Dictionary<string, FlowConnector>> nodes,
        FlowEndpoint endpoint,
        out FlowConnector connector)
    {
        if (nodes.TryGetValue(endpoint.NodeId, out var connectors)
            && connectors.TryGetValue(endpoint.ConnectorId, out var found))
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