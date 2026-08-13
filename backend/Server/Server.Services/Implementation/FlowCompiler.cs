using Server.Services.Contracts;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

public sealed partial class FlowCompiler : IFlowCompiler
{
    private const int MaximumArtifactBytes = 8192;
    private const ulong ExpandedBooleanCapability = 1UL << 5;

    private static readonly IReadOnlyDictionary<string, NodeShape> Shapes =
        new Dictionary<string, NodeShape>(StringComparer.Ordinal)
        {
            ["digitalInput"] = new([new("value", 2)]),
            ["digitalConstant"] = new([new("value", 2)]),
            ["not"] = new([new("in", 1), new("value", 2)]),
            ["and"] = new([new("a", 1), new("b", 1), new("value", 2)]),
            ["or"] = new([new("a", 1), new("b", 1), new("value", 2)]),
            ["nand"] = new([new("a", 1), new("b", 1), new("value", 2)]),
            ["nor"] = new([new("a", 1), new("b", 1), new("value", 2)]),
            ["xor"] = new([new("a", 1), new("b", 1), new("value", 2)]),
            ["xnor"] = new([new("a", 1), new("b", 1), new("value", 2)]),
            ["memory"] = new([new("in", 1), new("value", 2)]),
            ["digitalOutput"] = new([new("in", 1)])
        };

    public FlowCompilationResult Compile(FlowCompilationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArtifactVersion != 2)
        {
            throw Failure(
                "unsupported_artifact_version",
                "/artifactVersion",
                "Only the current Flow IL version 2 is supported during pre-release development.");
        }

        Validate(request);
        return CompileFlowIlV2(request);
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

    private static FlowCompilationResult CompileFlowIlV2(FlowCompilationRequest request)
    {
        const int envelopeLength = 128;
        const int directoryEntryLength = 48;
        var source = request.Source;
        var schedule = GetSchedule(source);
        var nodes = source.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var slots = schedule.Select((id, index) => new { id, index = checked((ushort)index) })
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        var memoryIds = schedule.Where(id => nodes[id].Kind == "memory").ToArray();
        var stateSlots = memoryIds.Select((id, index) => new
        {
            id,
            index = checked((ushort)(schedule.Count + index))
        })
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        var points = BuildPoints([.. schedule.Select(id => nodes[id])]);
        var pointIndices = points.Select((point, index) => new { point, index = checked((ushort)index) })
            .ToDictionary(item => item.point, item => item.index);
        var constants = source.Nodes.Where(node => node.Kind is "digitalConstant" or "memory")
            .Select(node => node.Configuration["value"].GetBoolean())
            .Distinct()
            .Order()
            .ToArray();
        var instructions = new List<V2Instruction>();

        foreach (var id in schedule)
        {
            var node = nodes[id];
            var result = slots[id];
            instructions.Add(node.Kind switch
            {
                "digitalInput" => new(1, result, ushort.MaxValue, ushort.MaxValue,
                    pointIndices[new PointRecord(node.Configuration["pointId"].GetString()!, 1)], id, 0),
                "digitalConstant" => new(2, result, ushort.MaxValue, ushort.MaxValue,
                    checked((ushort)Array.IndexOf(constants, node.Configuration["value"].GetBoolean())), id, 0),
                "not" => new(3, result, InputSlot(source, slots, id, "in"), ushort.MaxValue, ushort.MaxValue, id, 0),
                "and" => new(4, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ushort.MaxValue, id, 0),
                "or" => new(5, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ushort.MaxValue, id, 0),
                "nand" => new(9, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ushort.MaxValue, id, 0),
                "nor" => new(10, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ushort.MaxValue, id, 0),
                "xor" => new(11, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ushort.MaxValue, id, 0),
                "xnor" => new(12, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ushort.MaxValue, id, 0),
                "memory" => new(6, result, ushort.MaxValue, ushort.MaxValue, stateSlots[id], id, 0),
                "digitalOutput" => new(7, result, InputSlot(source, slots, id, "in"), ushort.MaxValue,
                    pointIndices[new PointRecord(node.Configuration["pointId"].GetString()!, 2)], id, 0),
                _ => throw new UnreachableException()
            });
        }

        foreach (var id in memoryIds)
        {
            instructions.Add(new V2Instruction(
                8,
                ushort.MaxValue,
                InputSlot(source, slots, id, "in"),
                ushort.MaxValue,
                stateSlots[id],
                id,
                1));
        }

        instructions.Add(new V2Instruction(
            byte.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            string.Empty,
            0));

        var constantSection = Concat([.. constants.Select(value => new byte[] { 1, value ? (byte)1 : (byte)0, 0, 0 })]);
        var pointSection = Concat([.. points.Select(point => Concat(
            new byte[] { point.Direction, 1, 1, 0 },
            String8(point.Id)))]);
        var slotRecords = schedule.Select((_, index) => Concat(
            new byte[] { 2, 1 }, U16(0), U16(index), U16(ushort.MaxValue))).ToList();
        slotRecords.AddRange(memoryIds.Select(id => Concat(
            new byte[] { 3, 1 },
            U16(0),
            U16(stateSlots[id]),
            U16(Array.IndexOf(constants, nodes[id].Configuration["value"].GetBoolean())))));
        var slotSection = Concat([.. slotRecords]);
        var instructionSection = Concat([.. instructions.Select(EncodeV2Instruction)]);
        var commitRecords = memoryIds.Select(id => Concat(
            new byte[] { 1, 0 }, U16(stateSlots[id]), U16(InputSlot(source, slots, id, "in")), U16(0))).ToList();
        commitRecords.AddRange(schedule.Where(id => nodes[id].Kind == "digitalOutput").Select(id => Concat(
            new byte[] { 2, 0 },
            U16(pointIndices[new PointRecord(nodes[id].Configuration["pointId"].GetString()!, 2)]),
            U16(slots[id]),
            U16(0))));
        var symbolSection = Concat([.. instructions.Select((instruction, index) => Concat(
            U16(index), new byte[] { instruction.Discriminator }, String8AllowEmpty(instruction.NodeId)))]);
        var debugSection = Concat([.. instructions.Where(instruction => instruction.NodeId.Length > 0).Select((instruction, index) => Concat(U16(index), U16(instruction.Result), String8(instruction.NodeId)))]);
        var resolvedPoints = request.Target.Points
            .GroupBy(point => point.Id, StringComparer.Ordinal)
            .Select(group => group.Single())
            .OrderBy(point => point.Id, StringComparer.Ordinal)
            .ToArray();
        var dependencyRecords = new List<byte[]>
        {
            Concat(new byte[] { 1 }, String8(source.ControllerTemplateId), U32(source.ControllerTemplateRevision))
        };
        dependencyRecords.AddRange(points.Select(point => point.Id).Distinct(StringComparer.Ordinal).Select(pointId =>
        {
            var resolved = resolvedPoints.SingleOrDefault(candidate => candidate.Id == pointId);
            if (resolved is null)
            {
                throw Failure("missing_point", $"/points/{Escape(pointId)}", "Resolved point dependency is missing.");
            }

            var revision = resolved.Revision;
            if (revision <= 0)
            {
                throw Failure("invalid_dependency", $"/points/{Escape(pointId)}/revision", "Point revision must be positive.");
            }

            return Concat(new byte[] { 2 }, String8(pointId), U32(checked((uint)revision)));
        }));
        var dependencySection = Concat(dependencyRecords.ToArray());
        V2Section[] sections =
        [
            new(1, checked((uint)constants.Length), constantSection),
            new(2, checked((uint)points.Length), pointSection),
            new(3, checked((uint)slotRecords.Count), slotSection),
            new(4, checked((uint)instructions.Count), instructionSection),
            new(5, checked((uint)commitRecords.Count), Concat([.. commitRecords])),
            new(6, checked((uint)instructions.Count), symbolSection),
            new(7, checked((uint)(instructions.Count - 1)), debugSection),
            new(8, checked((uint)dependencyRecords.Count), dependencySection)
        ];
        var offset = checked((uint)(envelopeLength + (sections.Length * directoryEntryLength)));
        var directory = new List<byte[]>();
        foreach (var section in sections)
        {
            directory.Add(Concat(
                U16(section.Id),
                U16(1),
                U32(offset),
                U32(checked((uint)section.Bytes.Length)),
                U32(section.Count),
                SHA256.HashData(section.Bytes)));
            offset += checked((uint)section.Bytes.Length);
        }

        if (offset > MaximumArtifactBytes)
        {
            throw Failure("limit_exceeded", "/artifactLength", "Encoded Flow IL exceeds 8192 bytes.");
        }

        var capabilities = 1UL | 16UL;
        if (points.Any(point => point.Direction == 1))
        {
            capabilities |= 2UL;
        }

        if (points.Any(point => point.Direction == 2))
        {
            capabilities |= 4UL;
        }

        if (memoryIds.Length > 0)
        {
            capabilities |= 8UL;
        }

        if (source.Nodes.Any(node => node.Kind is "nand" or "nor" or "xor" or "xnor"))
        {
            capabilities |= ExpandedBooleanCapability;
        }

        var workingBytes = checked((uint)((schedule.Count + memoryIds.Length) * 2));
        var envelope = new byte[envelopeLength];
        "FIL2"u8.CopyTo(envelope);
        WriteU16(envelope, 4, 2);
        WriteU16(envelope, 6, envelopeLength);
        WriteU32(envelope, 8, offset);
        WriteU32(envelope, 12, 1);
        WriteU32(envelope, 16, source.Revision);
        WriteU32(envelope, 20, source.ControllerTemplateRevision);
        WriteU16(envelope, 24, 1);
        WriteU16(envelope, 26, sections.Length);
        envelope[28] = 1;
        WriteU32(envelope, 32, checked((uint)instructions.Count));
        BinaryPrimitives.WriteUInt64LittleEndian(envelope.AsSpan(36), capabilities);
        WriteU32(envelope, 44, workingBytes);
        WriteU32(envelope, 48, 16384);
        WritePaddedIdentifier(envelope, 52, source.Id);
        WriteU32(envelope, 116, envelopeLength);
        var artifact = Concat(envelope, Concat([.. directory]), Concat([.. sections.Select(section => section.Bytes)]));

        return new FlowCompilationResult
        {
            ArtifactVersion = 2,
            Artifact = artifact,
            ArtifactSha256 = Convert.ToHexStringLower(SHA256.HashData(artifact)),
            FlowRevision = source.Revision,
            ControllerTemplateId = source.ControllerTemplateId,
            ControllerTemplateRevision = checked((int)source.ControllerTemplateRevision),
            NodeIndices = slots,
            Schedule = schedule,
            MaximumWorkPerScan = checked((uint)instructions.Count),
            WorkingBytes = workingBytes,
            MaximumSnapshotBytes = 16384
        };
    }

    private static IReadOnlyList<string> GetSchedule(ExecutableFlowSource source)
    {
        var nodes = source.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var indegree = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var outgoing = nodes.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var connection in source.Connections)
        {
            if (nodes[connection.Target.NodeId].Kind == "memory")
            {
                continue;
            }

            indegree[connection.Target.NodeId]++;
            outgoing[connection.Source.NodeId].Add(connection.Target.NodeId);
        }

        var ready = new SortedSet<string>(indegree.Where(item => item.Value == 0).Select(item => item.Key), StringComparer.Ordinal);
        var result = new List<string>(nodes.Count);
        while (ready.Count > 0)
        {
            var id = ready.Min!;
            ready.Remove(id);
            result.Add(id);
            foreach (var target in outgoing[id].Order(StringComparer.Ordinal))
            {
                if (--indegree[target] == 0)
                {
                    ready.Add(target);
                }
            }
        }

        return result;
    }

    private static ushort InputSlot(
        ExecutableFlowSource source,
        IReadOnlyDictionary<string, ushort> slots,
        string targetId,
        string portId) => slots[source.Connections.Single(connection =>
            connection.Target.NodeId == targetId && connection.Target.PortId == portId).Source.NodeId];

    private static byte[] EncodeV2Instruction(V2Instruction instruction) => Concat(
        new byte[] { instruction.Opcode, 0 },
        U16(instruction.Result),
        U16(instruction.Operand0),
        U16(instruction.Operand1),
        U16(instruction.Auxiliary),
        U16(0));

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

    private static byte[] String8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat(new byte[] { checked((byte)bytes.Length) }, bytes);
    }

    private static byte[] String8AllowEmpty(string value)
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

    private sealed record NodeShape(IReadOnlyList<PortShape> Ports);
    private sealed record PortShape(string Id, byte Direction);
    private sealed record PortKey(string NodeId, string PortId);
    private sealed record PointRecord(string Id, byte Direction);
    private sealed record V2Instruction(
        byte Opcode,
        ushort Result,
        ushort Operand0,
        ushort Operand1,
        ushort Auxiliary,
        string NodeId,
        byte Discriminator);
    private sealed record V2Section(ushort Id, uint Count, byte[] Bytes);
}
