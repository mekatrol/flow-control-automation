using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

internal static partial class FlowValidator
{
    private static readonly HashSet<string> ValidKinds =
    [
        "add", "analogInput", "analogOutput", "and", "average", "calculator", "calendar", "clamp", "comparator",
        "delay", "digitalConstant", "digitalInput", "digitalOutput", "if",
        "levelShifter", "line", "max", "memory", "min", "nand", "nor", "not", "numericConstant", "or", "override",
        "pulse", "schedule", "selector", "sequence", "split", "flowInput", "flowOutput",
        "onDelay", "qualityGood", "risingEdge", "timer", "xnor", "xor",
    ];

    private static readonly HashSet<string> ValidStatuses = ["draft", "deployed"];

    private static readonly HashSet<DataDirection> ValidDirections = [DataDirection.Input, DataDirection.Output];

    private static readonly HashSet<DataType> ValidDataTypes =
        [DataType.Any, DataType.Boolean, DataType.Event, DataType.Number, DataType.String];

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

        ValidateInterface(flow.Interface);

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

            ValidateInterfaceNode(flow.Interface, node, nodeIndex);

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

            if (start.Direction != DataDirection.Output || end.Direction != DataDirection.Input)
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

    private static void ValidateInterface(FlowInterface definition)
    {
        if (definition.SchemaVersion != 1)
        {
            throw new FlowValidationException("interface.schemaVersion: only version 1 is supported");
        }

        if (definition.Inputs.Count > 64 || definition.Outputs.Count > 64)
        {
            throw new FlowValidationException("interface: at most 64 inputs and 64 outputs are supported");
        }

        ValidateEntries(definition.Inputs.Select(entry => (entry.Id, entry.Name, entry.DataType, entry.Units, entry.DefaultValue)), "interface.inputs", true);
        ValidateEntries(definition.Outputs.Select(entry => (entry.Id, entry.Name, entry.DataType, entry.Units, (JsonElement?)null)), "interface.outputs", false);
    }

    private static void ValidateEntries(
        IEnumerable<(string Id, string Name, DataType DataType, string? Units, JsonElement? DefaultValue)> values,
        string path,
        bool inputs)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (entry, index) in values.Select((entry, index) => (entry, index)))
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name) || !ids.Add(entry.Id) || !names.Add(entry.Name))
            {
                throw new FlowValidationException($"{path}[{index}]: id and name must be non-empty and unique");
            }

            if (entry.DataType is not (DataType.Boolean or DataType.Number or DataType.String or DataType.Event))
            {
                throw new FlowValidationException($"{path}[{index}].dataType: unsupported type");
            }

            if (entry.DataType != DataType.Number && !string.IsNullOrEmpty(entry.Units))
            {
                throw new FlowValidationException($"{path}[{index}].units: units require number data type");
            }

            if (Encoding.UTF8.GetByteCount(entry.Id) > 63 || Encoding.UTF8.GetByteCount(entry.Name) > 255 || (entry.Units is not null && Encoding.UTF8.GetByteCount(entry.Units) > 63))
            {
                throw new FlowValidationException($"{path}[{index}]: text exceeds interface bounds");
            }

            if (inputs && entry.DefaultValue is { } value && !Matches(value, entry.DataType))
            {
                throw new FlowValidationException($"{path}[{index}].defaultValue: value does not match dataType");
            }
        }
    }

    private static bool Matches(JsonElement value, DataType dataType) => dataType switch
    {
        DataType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        DataType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number),
        DataType.String => value.ValueKind == JsonValueKind.String,
        DataType.Event => value.ValueKind == JsonValueKind.Null,
        _ => false
    };

    private static void ValidateInterfaceNode(FlowInterface definition, FlowNode node, int nodeIndex)
    {
        if (node.Kind is not ("flowInput" or "flowOutput"))
        {
            return;
        }

        if (node.Configuration.Count != 1 || !node.Configuration.TryGetValue("interfaceId", out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new FlowValidationException($"nodes[{nodeIndex}].configuration.interfaceId: required string");
        }

        var id = value.GetString();
        var entry = node.Kind == "flowInput"
            ? definition.Inputs.Select(item => (item.Id, item.Name, item.DataType, item.Units)).SingleOrDefault(item => item.Id == id)
            : definition.Outputs.Select(item => (item.Id, item.Name, item.DataType, item.Units)).SingleOrDefault(item => item.Id == id);
        if (entry.Id is null)
        {
            throw new FlowValidationException($"nodes[{nodeIndex}].configuration.interfaceId: unknown interface entry");
        }

        var expectedDirection = node.Kind == "flowInput" ? DataDirection.Output : DataDirection.Input;
        if (node.Connectors.Count != 1 || node.Connectors[0].Direction != expectedDirection || node.Connectors[0].DataType != entry.DataType)
        {
            throw new FlowValidationException($"nodes[{nodeIndex}].connectors: terminal connector does not match interface entry");
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