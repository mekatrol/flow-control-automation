using Server.Services.Contracts;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

public sealed partial class FlowCompiler : IFlowCompiler
{
    private const int EnvelopeLength = 192;
    private const int MaximumArtifactBytes = 8192;
    private const uint MaximumSnapshotBytes = 4096;

    private static readonly IReadOnlyDictionary<string, NodeShape> Shapes =
        new Dictionary<string, NodeShape>(StringComparer.Ordinal)
        {
            ["digitalInput"] = new(1, [new("value", 2)]),
            ["digitalConstant"] = new(2, [new("value", 2)]),
            ["not"] = new(3, [new("in", 1), new("value", 2)]),
            ["and"] = new(4, [new("a", 1), new("b", 1), new("value", 2)]),
            ["or"] = new(5, [new("a", 1), new("b", 1), new("value", 2)]),
            ["memory"] = new(6, [new("in", 1), new("value", 2)]),
            ["digitalOutput"] = new(7, [new("in", 1)])
        };

    public FlowCompilationResult Compile(FlowCompilationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var source = request.Source;
        var nodes = source.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
        var nodeIndices = nodes
            .Select((node, index) => new { node.Id, Index = checked((ushort)index) })
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.Ordinal);
        var points = BuildPoints(nodes);
        var ports = BuildPorts(nodes);
        var portIndices = ports
            .Select((port, index) => new { port.Key, Index = checked((ushort)index) })
            .ToDictionary(item => item.Key, item => item.Index);
        var connections = source.Connections
            .OrderBy(connection => connection.Target.NodeId, StringComparer.Ordinal)
            .ThenBy(connection => connection.Target.PortId, StringComparer.Ordinal)
            .ThenBy(connection => connection.Source.NodeId, StringComparer.Ordinal)
            .ThenBy(connection => connection.Source.PortId, StringComparer.Ordinal)
            .ToArray();

        var nodeTable = Table(nodes.Select(node => EncodeNode(node, points)));
        var portTable = Table(ports.Select(EncodePort));
        var connectionTable = Table(connections.Select(connection => EncodeConnection(
            connection,
            nodeIndices,
            portIndices)));
        var pointTable = Table(points.Select(EncodePoint));
        var body = EncodeBody(nodeTable, portTable, connectionTable, pointTable);
        var artifact = EncodeEnvelope(source, nodes, ports, connections, points, body);
        if (artifact.Length > MaximumArtifactBytes)
        {
            throw Failure("limit_exceeded", "/artifactLength", "Encoded artifact exceeds 8192 bytes.");
        }

        return new FlowCompilationResult
        {
            Artifact = artifact,
            ArtifactSha256 = Convert.ToHexStringLower(SHA256.HashData(artifact)),
            FlowRevision = source.Revision,
            ControllerTemplateId = source.ControllerTemplateId,
            ControllerTemplateRevision = checked((int)source.ControllerTemplateRevision),
            NodeIndices = nodeIndices
        };
    }

    private static void Validate(FlowCompilationRequest request)
    {
        var source = request.Source;
        if (source.SchemaVersion != 1)
        {
            throw Failure("unsupported_source_schema", "/schemaVersion", "Only source schema 1 is supported.");
        }

        ValidateIdentifier(source.Id, "/id", 63);
        ValidateIdentifier(source.ControllerTemplateId, "/controllerTemplateId", 31);
        if (source.Revision == 0)
        {
            throw Failure("invalid_source", "/revision", "Revision must be greater than zero.");
        }

        if (source.ControllerTemplateRevision == 0)
        {
            throw Failure(
                "invalid_source",
                "/controllerTemplateRevision",
                "Controller template revision must be greater than zero.");
        }

        var target = request.Target.ControllerTemplate.Source;
        if (!string.Equals(source.ControllerTemplateId, target.Id, StringComparison.Ordinal))
        {
            throw Failure("target_mismatch", "/controllerTemplateId", "Resolved target ID does not match source.");
        }

        if (target.Revision < 0 || (uint)target.Revision != source.ControllerTemplateRevision)
        {
            throw Failure(
                "target_mismatch",
                "/controllerTemplateRevision",
                "Resolved target revision does not match source.");
        }

        if (source.Execution.Mode != "manual"
            || source.Execution.IntervalMs != 0
            || source.Execution.InputQualityPolicy != "require_good")
        {
            throw Failure("unsupported_execution", "/execution", "Schema 1 supports manual require-good execution only.");
        }

        if (source.Nodes.Count is < 1 or > 128)
        {
            throw Failure("limit_exceeded", "/nodes", "Node count must be between 1 and 128.");
        }

        if (source.Connections.Count > 384)
        {
            throw Failure("limit_exceeded", "/connections", "Connection count exceeds 384.");
        }

        ValidateGraph(source);
    }

    private static void ValidateGraph(ExecutableFlowSource source)
    {
        var nodes = new Dictionary<string, ExecutableFlowNode>(StringComparer.Ordinal);
        var shapes = new Dictionary<string, IReadOnlyDictionary<string, PortShape>>(StringComparer.Ordinal);
        for (var index = 0; index < source.Nodes.Count; index++)
        {
            var node = source.Nodes[index];
            ValidateIdentifier(node.Id, $"/nodes/{index}/id", 63);
            if (!nodes.TryAdd(node.Id, node))
            {
                throw Failure("duplicate_node", $"/nodes/{index}/id", $"Node ID \"{node.Id}\" is duplicated.");
            }

            if (!Shapes.TryGetValue(node.Kind, out var shape))
            {
                throw Failure("unsupported_node", $"/nodes/{index}/kind", $"Node kind \"{node.Kind}\" is unsupported.");
            }

            ValidateConfiguration(node, index);
            shapes[node.Id] = shape.Ports.ToDictionary(port => port.Id, StringComparer.Ordinal);
        }

        var drivers = new HashSet<PortKey>();
        foreach (var (connection, index) in source.Connections.Select((value, index) => (value, index)))
        {
            var sourcePort = FindPort(nodes, shapes, connection.Source, index, "source");
            var targetPort = FindPort(nodes, shapes, connection.Target, index, "target");
            if (sourcePort.Direction != 2 || targetPort.Direction != 1)
            {
                throw Failure("invalid_endpoint", $"/connections/{index}", "Connection must run from output to input.");
            }

            if (!drivers.Add(new(connection.Target.NodeId, connection.Target.PortId)))
            {
                throw Failure("duplicate_driver", $"/connections/{index}/target", "Input already has a driver.");
            }
        }

        foreach (var node in source.Nodes)
        {
            foreach (var input in Shapes[node.Kind].Ports.Where(port => port.Direction == 1))
            {
                if (!drivers.Contains(new(node.Id, input.Id)))
                {
                    throw Failure(
                        "missing_connection",
                        $"/nodes/{Escape(node.Id)}/ports/{Escape(input.Id)}",
                        "Input has no driver.");
                }
            }
        }

        ValidatePointReferences(source.Nodes);
        ValidateAcyclic(source, nodes);
    }

    private static void ValidateConfiguration(ExecutableFlowNode node, int index)
    {
        var path = $"/nodes/{index}/configuration";
        if (node.Kind is "digitalInput" or "digitalOutput")
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("pointId", out var point)
                || point.ValueKind != JsonValueKind.String
                || point.GetString() is not string pointId)
            {
                throw Failure("invalid_configuration", path, "A pointId string is required.");
            }

            ValidateIdentifier(pointId, $"{path}/pointId", 63);
        }
        else if (node.Kind is "digitalConstant" or "memory")
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("value", out var value)
                || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Failure("invalid_configuration", path, "A Boolean value is required.");
            }
        }
        else if (node.Configuration.Count != 0)
        {
            throw Failure("invalid_configuration", path, "This node requires empty configuration.");
        }
    }

    private static PortShape FindPort(
        IReadOnlyDictionary<string, ExecutableFlowNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, PortShape>> shapes,
        ExecutableFlowEndpoint endpoint,
        int connectionIndex,
        string endpointName)
    {
        if (!nodes.ContainsKey(endpoint.NodeId)
            || !shapes[endpoint.NodeId].TryGetValue(endpoint.PortId, out var port))
        {
            throw Failure(
                "invalid_endpoint",
                $"/connections/{connectionIndex}/{endpointName}",
                "Endpoint does not exist.");
        }

        return port;
    }

    private static void ValidatePointReferences(IReadOnlyList<ExecutableFlowNode> nodes)
    {
        var outputPoints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes.Where(node => node.Kind == "digitalOutput"))
        {
            var pointId = node.Configuration["pointId"].GetString()!;
            if (!outputPoints.Add(pointId))
            {
                throw Failure(
                    "duplicate_driver",
                    $"/points/{Escape(pointId)}",
                    "Only one proposed-output node may target a point.");
            }
        }
    }

    private static void ValidateAcyclic(
        ExecutableFlowSource source,
        IReadOnlyDictionary<string, ExecutableFlowNode> nodes)
    {
        var indegree = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var outgoing = nodes.Keys.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var connection in source.Connections)
        {
            if (nodes[connection.Target.NodeId].Kind == "memory")
            {
                continue;
            }

            indegree[connection.Target.NodeId]++;
            outgoing[connection.Source.NodeId].Add(connection.Target.NodeId);
        }

        var ready = new SortedSet<string>(
            indegree.Where(item => item.Value == 0).Select(item => item.Key),
            StringComparer.Ordinal);
        var visited = 0;
        while (ready.Count > 0)
        {
            var id = ready.Min!;
            ready.Remove(id);
            visited++;
            foreach (var target in outgoing[id])
            {
                indegree[target]--;
                if (indegree[target] == 0)
                {
                    ready.Add(target);
                }
            }
        }

        if (visited != nodes.Count)
        {
            var first = indegree.Where(item => item.Value > 0)
                .Select(item => item.Key)
                .Min(StringComparer.Ordinal)!;
            throw Failure("combinational_cycle", $"/nodes/{Escape(first)}", "Graph contains a combinational cycle.");
        }
    }

    private static PointRecord[] BuildPoints(IReadOnlyList<ExecutableFlowNode> nodes) => [.. nodes
        .Where(node => node.Kind is "digitalInput" or "digitalOutput")
        .Select(node => new PointRecord(
            node.Configuration["pointId"].GetString()!,
            checked((byte)(node.Kind == "digitalInput" ? 1 : 2))))
        .Distinct()
        .OrderBy(point => point.Id, StringComparer.Ordinal)
        .ThenBy(point => point.Direction)];

    private static PortRecord[] BuildPorts(IReadOnlyList<ExecutableFlowNode> nodes) => [.. nodes
        .SelectMany((node, nodeIndex) => Shapes[node.Kind].Ports.Select(port => new PortRecord(
            checked((ushort)nodeIndex),
            node.Id,
            port.Id,
            port.Direction)))];

    private static byte[] EncodeNode(ExecutableFlowNode node, IReadOnlyList<PointRecord> points)
    {
        var configuration = node.Kind switch
        {
            "digitalInput" => U16(PointIndex(points, node, 1)),
            "digitalConstant" or "memory" =>
                new byte[] { node.Configuration["value"].GetBoolean() ? (byte)1 : (byte)0 },
            "digitalOutput" => Concat(
                U16(PointIndex(points, node, 2)),
                new byte[] { 1, 8 },
                U32(0)),
            _ => Array.Empty<byte>()
        };
        return Concat(
            String8(node.Id),
            new byte[] { Shapes[node.Kind].Opcode },
            U16(configuration.Length),
            configuration);
    }

    private static ushort PointIndex(
        IReadOnlyList<PointRecord> points,
        ExecutableFlowNode node,
        byte direction)
    {
        var pointId = node.Configuration["pointId"].GetString();
        return checked((ushort)points.Select((point, index) => new { point, index })
            .Single(item => item.point.Id == pointId && item.point.Direction == direction).index);
    }

    private static byte[] EncodePort(PortRecord port) => Concat(
        U16(port.NodeIndex),
        String8(port.PortId),
        new byte[] { port.Direction, 2, 1, 0 });

    private static byte[] EncodeConnection(
        ExecutableFlowConnection connection,
        IReadOnlyDictionary<string, ushort> nodeIndices,
        IReadOnlyDictionary<PortKey, ushort> portIndices) => Concat(
            U16(nodeIndices[connection.Source.NodeId]),
            U16(portIndices[new(connection.Source.NodeId, connection.Source.PortId)]),
            U16(nodeIndices[connection.Target.NodeId]),
            U16(portIndices[new(connection.Target.NodeId, connection.Target.PortId)]));

    private static byte[] EncodePoint(PointRecord point) =>
        Concat(String8(point.Id), new byte[] { point.Direction, 2, 1, 0 });

    private static byte[] EncodeBody(params byte[][] tables)
    {
        var offsets = new uint[tables.Length];
        var offset = 24u;
        for (var index = 0; index < tables.Length; index++)
        {
            offsets[index] = offset;
            offset += checked((uint)tables[index].Length);
        }

        byte[][] parts =
        [
            U32(offset),
            U32(offsets[0]),
            U32(offsets[1]),
            U32(offsets[2]),
            U32(offsets[3]),
            U32(0),
            .. tables
        ];
        return Concat(parts);
    }

    private static byte[] EncodeEnvelope(
        ExecutableFlowSource source,
        IReadOnlyList<ExecutableFlowNode> nodes,
        IReadOnlyList<PortRecord> ports,
        IReadOnlyList<ExecutableFlowConnection> connections,
        IReadOnlyList<PointRecord> points,
        byte[] body)
    {
        var envelope = new byte[EnvelopeLength];
        "FCEX"u8.CopyTo(envelope);
        WriteU16(envelope, 4, 1);
        WriteU16(envelope, 6, 1);
        WriteU16(envelope, 8, EnvelopeLength);
        WriteU32(envelope, 12, checked((uint)(EnvelopeLength + body.Length)));
        WriteU32(envelope, 16, source.Revision);
        WriteU32(envelope, 20, source.ControllerTemplateRevision);
        envelope[24] = 1;
        envelope[25] = 1;
        WriteU16(envelope, 32, nodes.Count);
        WriteU16(envelope, 34, ports.Count);
        WriteU16(envelope, 36, connections.Count);
        WriteU16(envelope, 38, points.Count);
        var capabilities = 1u | 2u | 16u;
        if (nodes.Any(node => node.Kind == "memory"))
        {
            capabilities |= 4;
        }

        if (nodes.Any(node => node.Kind == "digitalOutput"))
        {
            capabilities |= 8;
        }

        WriteU32(envelope, 40, capabilities);
        WriteU32(envelope, 44, MaximumSnapshotBytes);
        WritePaddedIdentifier(envelope, 48, source.Id);
        WritePaddedIdentifier(envelope, 112, source.ControllerTemplateId);
        SHA256.HashData(body).CopyTo(envelope, 160);
        return Concat(envelope, body);
    }

    private static byte[] Table(IEnumerable<byte[]> records)
    {
        var materialized = records.ToArray();
        byte[][] parts = [U16(materialized.Length), .. materialized];
        return Concat(parts);
    }

    private static byte[] String8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat(new byte[] { checked((byte)bytes.Length) }, bytes);
    }

    private static byte[] U16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
        return bytes;
    }

    private static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }

    private static void WriteU16(byte[] target, int offset, int value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset), checked((ushort)value));

    private static void WriteU32(byte[] target, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);

    private static void WritePaddedIdentifier(byte[] target, int offset, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        target[offset] = checked((byte)bytes.Length);
        bytes.CopyTo(target, offset + 1);
    }

    private static void ValidateIdentifier(string value, string path, int maximumBytes)
    {
        if (!IdentifierRegex().IsMatch(value) || Encoding.UTF8.GetByteCount(value) > maximumBytes)
        {
            throw Failure("invalid_identifier", path, "Identifier has invalid syntax or length.");
        }
    }

    private static FlowCompilationException Failure(string code, string path, string message) =>
        new([new FlowCompilationDiagnostic(code, path, message)]);

    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal)
        .Replace("/", "~1", StringComparison.Ordinal);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    private sealed record NodeShape(byte Opcode, IReadOnlyList<PortShape> Ports);
    private sealed record PortShape(string Id, byte Direction);
    private sealed record PortKey(string NodeId, string PortId);
    private sealed record PortRecord(ushort NodeIndex, string NodeId, string PortId, byte Direction)
    {
        public PortKey Key => new(NodeId, PortId);
    }

    private sealed record PointRecord(string Id, byte Direction);
}
