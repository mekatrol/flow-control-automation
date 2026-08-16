using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

public sealed partial class FlowCompiler : IFlowCompiler
{
    private const int MaximumArtifactBytes = 16384;
    private const ulong ExpandedBooleanCapability = 1UL << 5;
    private const ulong NumericCapability = 1UL << 6;
    private const ulong ComparisonCapability = 1UL << 7;
    private const ulong LevelShifterCapability = 1UL << 8;
    private const ulong QualityCapability = 1UL << 9;
    private const ulong TimerCapability = 1UL << 10;
    private const ulong EventCapability = 1UL << 11;

    private static readonly Dictionary<string, FlowPorts> Shapes = new(StringComparer.Ordinal)
    {
        ["digitalInput"] = new([new("value", DataDirection.Output, DataType.Boolean)]),
        ["analogInput"] = new([new("value", DataDirection.Output, DataType.Number)]),
        ["digitalConstant"] = new([new("value", DataDirection.Output, DataType.Boolean)]),
        ["not"] = new([new("in", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["and"] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["or"] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["nand"] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["nor"] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["xor"] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["xnor"] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["numericConstant"] = new([new("value", DataDirection.Output, DataType.Number)]),
        ["add"] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["comparator"] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Boolean)]),
        ["levelShifter"] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["qualityGood"] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Boolean)]),
        ["onDelay"] = new([new("in", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["risingEdge"] = new([new("in", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["memory"] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["flowInput"] = new([new("value", DataDirection.Output, DataType.Number)]),
        ["flowOutput"] = new([new("value", DataDirection.Output, DataType.Number)]),
        ["digitalOutput"] = new([new("in", DataDirection.Input, DataType.Boolean)]),
        ["analogOutput"] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["average"] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        ["calculator"] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        ["clamp"] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        ["min"] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["max"] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["line"] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        ["if"] = new([new("condition", DataDirection.Input, DataType.Boolean), new("whenTrue", DataDirection.Input, DataType.Boolean), new("whenFalse", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        ["selector"] = new([new("condition", DataDirection.Input, DataType.Boolean), new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["split"] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        ["sequence"] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        ["override"] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        ["delay"] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        ["timer"] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        ["pulse"] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        ["schedule"] = new([new("output", DataDirection.Output, DataType.Number)]),
        ["calendar"] = new([new("output", DataDirection.Output, DataType.Number)])
    };

    public FlowCompilationResult Compile(FlowCompilationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ArtifactVersion != 1)
        {
            throw Failure(
                "unsupported_artifact_version",
                "/artifactVersion",
                "Only Flow IL version 1 is supported.");
        }

        Validate(request);

        return CompileFlowIlV1(request);
    }

    public static void WriteBinary(FlowCompilationResult compilation, string path)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllBytes(path, compilation.Artifact.Span);
    }

    public static void WriteIntelHex(
        FlowCompilationResult compilation,
        string path,
        uint baseAddress = 0,
        int bytesPerRecord = 16)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (bytesPerRecord is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytesPerRecord),
                "Intel HEX records must contain between 1 and 255 bytes.");
        }

        using var writer = new StreamWriter(
            path,
            false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var artifact = compilation.Artifact.Span;

        var currentUpperAddress = uint.MaxValue;

        for (var offset = 0; offset < artifact.Length;)
        {
            var absoluteAddress = checked(baseAddress + (uint)offset);
            var upperAddress = absoluteAddress >> 16;

            if (upperAddress != currentUpperAddress)
            {
                Span<byte> upper =
                [
                    // Intel HEX represents the extended address most-significant byte first.
                    (byte)(upperAddress >> 8),
                    (byte)upperAddress,
                ];

                WriteIntelHexRecord(
                    writer,
                    0,
                    0x04,
                    upper);

                currentUpperAddress = upperAddress;
            }

            var address = checked((ushort)(absoluteAddress & 0xFFFF));

            // Do not allow a data record to cross a 64 KiB address boundary.
            var bytesUntilBoundary = 0x10000 - address;

            var count = Math.Min(
                bytesPerRecord,
                Math.Min(
                    artifact.Length - offset,
                    bytesUntilBoundary));

            WriteIntelHexRecord(
                writer,
                address,
                0x00,
                artifact.Slice(offset, count));

            offset += count;
        }

        // End-of-file record.
        WriteIntelHexRecord(
            writer,
            0,
            0x01,
            []);
    }

    private static void WriteIntelHexRecord(
        TextWriter writer,
        ushort address,
        byte recordType,
        ReadOnlySpan<byte> data)
    {
        var sum = data.Length
            + (address >> 8)
            + (address & 0xFF)
            + recordType;

        writer.Write(':');
        writer.Write(data.Length.ToString("X2"));
        writer.Write(address.ToString("X4"));
        writer.Write(recordType.ToString("X2"));

        foreach (var value in data)
        {
            writer.Write(value.ToString("X2"));
            sum += value;
        }

        var checksum = unchecked((byte)(-sum));

        writer.Write(checksum.ToString("X2"));

        // Explicit CR/LF rather than Environment.NewLine gives identical
        // output on Windows, Linux and macOS.
        writer.Write("\r\n");
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
            || source.Execution.InputQualityPolicy is not ("require_good" or "propagate"))
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

        ValidateInterface(source);

        ValidateGraph(source);
        ValidateUnits(request);
    }

    private static FlowCompilationResult CompileFlowIlV1(FlowCompilationRequest request)
    {
        const int envelopeLength = 128;
        const int directoryEntryLength = 48;
        var source = request.Source;
        var schedule = GetSchedule(source);
        var nodes = source.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

        var slots = schedule.Select((id, index) => new { id, index = checked((ushort)index) })
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);

        var memoryIds = schedule.Where(id => nodes[id].Kind == "memory").ToArray();
        var stateIds = schedule.Where(id => nodes[id].Kind is "memory" or "onDelay" or "risingEdge" or "delay" or "timer" or "pulse").ToArray();

        var stateSlots = stateIds.Select((id, index) => new
        {
            id,
            index = checked((ushort)(schedule.Count + index))
        }).ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);

        var points = BuildPoints(source, [.. schedule.Select(id => nodes[id])], request.Target.Points);

        var constants = source.Nodes.SelectMany(ConstantsFor)
            .Distinct()
            .OrderBy(constant => constant.DataType)
            .ThenBy(constant => constant.Number)
            .ToArray();

        var instructions = new List<V1Instruction>();

        foreach (var id in schedule)
        {
            var node = nodes[id];
            var result = slots[id];
            instructions.Add(node.Kind switch
            {
                "digitalInput" => new(1, result, ushort.MaxValue, ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Input, DataType.Boolean), id, 0),
                "analogInput" => new(1, result, ushort.MaxValue, ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Input, DataType.Number), id, 0),
                "flowInput" => new(1, result, ushort.MaxValue, ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Input, InterfaceDataType(source, node)), id, 0),
                "digitalConstant" => new(2, result, ushort.MaxValue, ushort.MaxValue,
                    ConstantIndex(constants, GetBooleanConstant(node.Configuration["value"].GetBoolean())), id, 0),
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
                "numericConstant" => new(13, result, ushort.MaxValue, ushort.MaxValue,
                    ConstantIndex(constants, GetNumericConstant(node, "value")), id, 0),
                "add" => new(14, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ushort.MaxValue, id, 0),
                "comparator" => new(15, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"),
                    ComparatorCode(node), id, 0),
                "levelShifter" => new(16, result, InputSlot(source, slots, id, "in"),
                    ConstantIndex(constants, GetNumericConstant(node, "gain")), ConstantIndex(constants, GetNumericConstant(node, "offset")), id, 0),
                "qualityGood" => new(17, result, InputSlot(source, slots, id, "in"), ushort.MaxValue,
                    ushort.MaxValue, id, 0),
                "onDelay" => new(18, result, InputSlot(source, slots, id, "in"), ushort.MaxValue,
                    stateSlots[id], id, 0),
                "risingEdge" => new(19, result, InputSlot(source, slots, id, "in"), ushort.MaxValue,
                    stateSlots[id], id, 0),
                "memory" => new(6, result, ushort.MaxValue, ushort.MaxValue, stateSlots[id], id, 0),
                "digitalOutput" => new(7, result, InputSlot(source, slots, id, "in"), ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Output, DataType.Boolean), id, 0),
                "analogOutput" => new(7, result, InputSlot(source, slots, id, "in"), ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Output, DataType.Number), id, 0),
                "flowOutput" => new(7, result, InputSlot(source, slots, id, "value"), ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Output, InterfaceDataType(source, node)), id, 0),
                "average" or "calculator" or "split" or "override" => new(24, result, InputSlot(source, slots, id, node.Kind is "split" or "override" ? "input" : "input"), ushort.MaxValue, ushort.MaxValue, id, 0),
                "min" => new(20, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"), ushort.MaxValue, id, 0),
                "max" => new(21, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"), ushort.MaxValue, id, 0),
                "clamp" => new(22, result, InputSlot(source, slots, id, "input"), ConstantIndex(constants, GetNumericConstant(node, "minimum")), ConstantIndex(constants, GetNumericConstant(node, "maximum")), id, 0),
                "line" => new(16, result, InputSlot(source, slots, id, "input"), ConstantIndex(constants, GetNumericConstant(node, "gain")), ConstantIndex(constants, GetNumericConstant(node, "offset")), id, 0),
                "if" => new(23, result, InputSlot(source, slots, id, "condition"), InputSlot(source, slots, id, "whenTrue"), InputSlot(source, slots, id, "whenFalse"), id, 0),
                "selector" => new(23, result, InputSlot(source, slots, id, "condition"), InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"), id, 0),
                "sequence" => new(4, result, InputSlot(source, slots, id, "a"), InputSlot(source, slots, id, "b"), ushort.MaxValue, id, 0),
                "delay" or "timer" => new(18, result, InputSlot(source, slots, id, "input"), ushort.MaxValue, stateSlots[id], id, 0),
                "pulse" => new(19, result, InputSlot(source, slots, id, "input"), ushort.MaxValue, stateSlots[id], id, 0),
                "schedule" or "calendar" => new(2, result, ushort.MaxValue, ushort.MaxValue, ConstantIndex(constants, GetBooleanConstant(node.Configuration["enabled"].GetBoolean())), id, 0),
                _ => throw new UnreachableException()
            });
        }

        foreach (var id in memoryIds)
        {
            instructions.Add(new V1Instruction(
                8,
                ushort.MaxValue,
                InputSlot(source, slots, id, "in"),
                ushort.MaxValue,
                stateSlots[id],
                id,
                1));
        }

        instructions.Add(new V1Instruction(
            byte.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            string.Empty,
            0));

        var constantSection = Concat([.. constants.Select(EncodeConstant)]);

        var pointSection = Concat([.. points.Select(point => Concat(
            [(byte)point.Direction, (byte)point.DataType, 1, point.Kind],
            String8(point.Id),
            String8AllowEmpty(point.Units ?? string.Empty)))]);

        var slotRecords = schedule.Select((id, index) => Concat(
            [2, (byte)ResultDataType(source, nodes[id])], U16(0), U16(index), U16(ushort.MaxValue))).ToList();

        slotRecords.AddRange(stateIds.Select(id => nodes[id].Kind switch
        {
            "memory" => Concat([3, 1], U16(0), U16(stateSlots[id]),
                U16(ConstantIndex(constants, GetNumericConstant(nodes[id], "value")))),

            "onDelay" or "delay" or "timer" => Concat([4, 1], U16(0), U16(stateSlots[id]),
                U16(ConstantIndex(constants, GetNumericConstant(nodes[id], "durationMs")))),

            "risingEdge" or "pulse" => Concat([5, 1], U16(0), U16(stateSlots[id]),
                U16(ConstantIndex(constants, GetBooleanConstant(false)))),
            _ => throw new UnreachableException()
        }));

        var slotSection = Concat([.. slotRecords]);
        var instructionSection = Concat([.. instructions.Select(EncodeV1Instruction)]);

        var commitRecords = memoryIds.Select(id => Concat([1, 0], U16(stateSlots[id]), U16(InputSlot(source, slots, id, "in")), U16(0))).ToList();

        commitRecords.AddRange(schedule.Where(id => nodes[id].Kind is "digitalOutput" or "analogOutput" or "flowOutput").Select(id => Concat(
            [2, 0],
            U16(PointIndex(points, nodes[id], DataDirection.Output, ResultDataType(source, nodes[id]))),
            U16(slots[id]),
            U16(0))));

        commitRecords.AddRange(stateIds.Where(id => nodes[id].Kind is "onDelay" or "risingEdge" or "delay" or "timer" or "pulse").Select(id => Concat(
            [1, 0], U16(stateSlots[id]), U16(slots[id]), U16(0))));

        var symbolSection = Concat([.. instructions.Select((instruction, index) =>
        {
            var authoring = instruction.NodeId.Length == 0 ? null : nodes[instruction.NodeId];
            return Concat(
                U16(index),
                [instruction.Discriminator],
                String8AllowEmpty(instruction.NodeId),
                String8AllowEmpty(authoring is null ? string.Empty : AuthoringLabel(authoring)),
                F64(authoring?.X ?? 0D),
                F64(authoring?.Y ?? 0D),
                F64(authoring?.ZOrder ?? 0D),
                String8AllowEmpty(authoring?.GroupId ?? string.Empty));
        })]);

        var debugSection = Concat([.. instructions.Where(instruction => instruction.NodeId.Length > 0).Select((instruction, index) => Concat(U16(index), U16(instruction.Result), String8(instruction.NodeId)))]);
        var resolvedPoints = request.Target.Points
            .GroupBy(point => point.Id, StringComparer.Ordinal)
            .Select(group => group.Single())
            .OrderBy(point => point.Id, StringComparer.Ordinal)
            .ToArray();

        var dependencyRecords = new List<byte[]>
        {
            Concat([1], String8(source.ControllerTemplateId), U32(source.ControllerTemplateRevision))
        };

        dependencyRecords.AddRange(points.Where(point => point.Kind == 0).Select(point => point.Id).Distinct(StringComparer.Ordinal).Select(pointId =>
        {
            var resolved = resolvedPoints.SingleOrDefault(candidate => candidate.Id == pointId) ?? throw Failure("missing_point", $"/points/{Escape(pointId)}", "Resolved point dependency is missing.");
            var revision = resolved.Revision;
            if (revision <= 0)
            {
                throw Failure("invalid_dependency", $"/points/{Escape(pointId)}/revision", "Point revision must be positive.");
            }

            return Concat([2], String8(pointId), U32(checked((uint)revision)));
        }));

        var dependencySection = Concat([.. dependencyRecords]);

        V1Section[] sections =
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
                U16(section.Version),
                U32(offset),
                U32(checked((uint)section.Bytes.Length)),
                U32(section.Count),
                SHA256.HashData(section.Bytes)));
            offset += checked((uint)section.Bytes.Length);
        }

        if (offset > MaximumArtifactBytes)
        {
            throw Failure("limit_exceeded", "/artifactLength", "Encoded Flow IL exceeds 16384 bytes.");
        }

        var capabilities = 1UL | 16UL;

        if (points.Any(point => point.Direction == DataDirection.Input))
        {
            capabilities |= 2UL;
        }

        if (points.Any(point => point.Direction == DataDirection.Output))
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

        if (source.Nodes.Any(node =>
            node.Kind is "numericConstant" or "add" or "comparator" or "levelShifter" or "average" or "calculator" or "clamp" or "min" or "max" or "line" or "selector") ||
            points.Any(point => point.DataType == DataType.Number))
        {
            capabilities |= NumericCapability;
        }

        if (source.Nodes.Any(node => node.Kind == "comparator"))
        {
            capabilities |= ComparisonCapability;
        }

        if (source.Nodes.Any(node => node.Kind == "levelShifter"))
        {
            capabilities |= LevelShifterCapability;
        }

        if (source.Nodes.Any(node => node.Kind == "qualityGood"))
        {
            capabilities |= QualityCapability;
        }

        if (source.Nodes.Any(node => node.Kind is "onDelay" or "delay" or "timer"))
        {
            capabilities |= TimerCapability;
        }

        if (source.Nodes.Any(node => node.Kind is "risingEdge" or "pulse"))
        {
            capabilities |= EventCapability;
        }

        var workingBytes = checked((uint)((schedule.Count + stateIds.Length) * 32));
        var envelope = new byte[envelopeLength];
        "FIL1"u8.CopyTo(envelope);
        WriteU16(envelope, 4, 1);
        WriteU16(envelope, 6, envelopeLength);
        WriteU32(envelope, 8, offset);
        WriteU32(envelope, 12, 1);
        WriteU32(envelope, 16, source.Revision);
        WriteU32(envelope, 20, source.ControllerTemplateRevision);
        WriteU16(envelope, 24, 1);
        WriteU16(envelope, 26, sections.Length);
        envelope[28] = source.Execution.InputQualityPolicy == "require_good" ? (byte)1 : (byte)2;
        WriteU32(envelope, 32, checked((uint)instructions.Count));
        BinaryPrimitives.WriteUInt64LittleEndian(envelope.AsSpan(36), capabilities);
        WriteU32(envelope, 44, workingBytes);
        WriteU32(envelope, 48, 16384);
        WritePaddedIdentifier(envelope, 52, source.Id);
        WriteU32(envelope, 116, envelopeLength);
        var artifact = Concat(envelope, Concat([.. directory]), Concat([.. sections.Select(section => section.Bytes)]));

        return new FlowCompilationResult
        {
            ArtifactVersion = 1,
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

    private static List<string> GetSchedule(ExecutableFlowSource source)
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
        Dictionary<string, ushort> slots,
        string targetId,
        string portId) => slots[source.Connections.Single(connection =>
            connection.Target.NodeId == targetId && connection.Target.PortId == portId).Source.NodeId];

    private static ushort PointIndex(IReadOnlyList<PointRecord> points, ExecutableFlowNode node, DataDirection direction, DataType type) =>
        checked((ushort)points.Select((point, index) => new { point, index }).Single(item =>
            item.point.Id == node.Configuration[node.Kind is "flowInput" or "flowOutput" ? "interfaceId" : "pointId"].GetString()
            && item.point.Direction == direction
            && item.point.DataType == type
            && item.point.Kind == (node.Kind is "flowInput" or "flowOutput" ? 1 : 0)).index);

    private static byte[] EncodeV1Instruction(V1Instruction instruction) => Concat(
        [instruction.Opcode, 0],
        U16(instruction.Result),
        U16(instruction.Operand0),
        U16(instruction.Operand1),
        U16(instruction.Auxiliary),
        U16(0));

    private static void ValidateGraph(ExecutableFlowSource source)
    {
        var nodes = new Dictionary<string, ExecutableFlowNode>(StringComparer.Ordinal);
        var shapes = new Dictionary<string, IReadOnlyDictionary<string, FlowPort>>(StringComparer.Ordinal);

        for (var index = 0; index < source.Nodes.Count; index++)
        {
            var node = source.Nodes[index];
            ValidateIdentifier(node.Id, $"/nodes/{index}/id", 63);
            if (Encoding.UTF8.GetByteCount(node.Label) > 255 || !double.IsFinite(node.X) || !double.IsFinite(node.Y) || !double.IsFinite(node.ZOrder))
            {
                throw Failure("invalid_authoring_metadata", $"/nodes/{index}", "Label and canvas coordinates exceed authoring metadata bounds.");
            }

            if (node.GroupId is { Length: > 0 } groupId)
            {
                ValidateIdentifier(groupId, $"/nodes/{index}/groupId", 63);
            }
            if (!nodes.TryAdd(node.Id, node))
            {
                throw Failure("duplicate_node", $"/nodes/{index}/id", $"Node ID \"{node.Id}\" is duplicated.");
            }

            if (!Shapes.TryGetValue(node.Kind, out var shape))
            {
                throw Failure("unsupported_node", $"/nodes/{index}/kind", $"Node kind \"{node.Kind}\" is unsupported.");
            }

            ValidateConfiguration(source, node, index);
            shapes[node.Id] = node.Kind switch
            {
                "flowInput" => new[] { new FlowPort("value", DataDirection.Output, InterfaceDataType(source, node)) }.ToDictionary(port => port.Id, StringComparer.Ordinal),
                "flowOutput" => new[] { new FlowPort("value", DataDirection.Input, InterfaceDataType(source, node)) }.ToDictionary(port => port.Id, StringComparer.Ordinal),
                _ => shape.Ports.ToDictionary(port => port.Id, StringComparer.Ordinal)
            };
        }

        var drivers = new HashSet<FlowPortKey>();

        var connections = source.Connections.Select((value, index) => (value, index)).ToArray();
        foreach (var (connection, index) in connections)
        {
            var sourcePort = FindPort(nodes, shapes, connection.Source, index, "source");
            var targetPort = FindPort(nodes, shapes, connection.Target, index, "target");

            if (sourcePort.Direction != DataDirection.Output || targetPort.Direction != DataDirection.Input)
            {
                throw Failure("invalid_endpoint", $"/connections/{index}", "Connection must run from output to input.");
            }

            if (sourcePort.DataType != targetPort.DataType)
            {
                throw Failure("type_mismatch", $"/connections/{index}", "Connected ports require the same value type.");
            }

            if (!drivers.Add(new(connection.Target.NodeId, connection.Target.PortId)))
            {
                throw Failure("duplicate_driver", $"/connections/{index}/target", "Input already has a driver.");
            }
        }

        foreach (var node in source.Nodes)
        {
            foreach (var input in shapes[node.Id].Values.Where(port => port.Direction == DataDirection.Input))
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

    private static void ValidateInterface(ExecutableFlowSource source)
    {
        if (source.Interface.SchemaVersion != 1)
        {
            throw Failure("unsupported_interface_schema", "/interface/schemaVersion", "Only interface schema 1 is supported.");
        }

        if (source.Interface.Inputs.Count > 64 || source.Interface.Outputs.Count > 64)
        {
            throw Failure("limit_exceeded", "/interface", "At most 64 interface inputs and outputs are supported.");
        }

        ValidateInterfaceEntries(source.Interface.Inputs.Select(entry => new InterfaceRecord(entry.Id, entry.Name, entry.DataType, entry.Units, entry.DefaultValue)), "/interface/inputs");
        ValidateInterfaceEntries(source.Interface.Outputs.Select(entry => new InterfaceRecord(entry.Id, entry.Name, entry.DataType, entry.Units, null)), "/interface/outputs");
    }

    private static void ValidateInterfaceEntries(IEnumerable<InterfaceRecord> entries, string path)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (entry, index) in entries.Select((entry, index) => (entry, index)))
        {
            ValidateIdentifier(entry.Id, $"{path}/{index}/id", 63);
            if (string.IsNullOrWhiteSpace(entry.Name) || Encoding.UTF8.GetByteCount(entry.Name) > 255 || !ids.Add(entry.Id) || !names.Add(entry.Name))
            {
                throw Failure("invalid_interface", $"{path}/{index}", "Interface IDs and names must be non-empty, bounded, and unique.");
            }

            if (entry.DataType is not (DataType.Boolean or DataType.Number))
            {
                throw Failure("unsupported_interface_type", $"{path}/{index}/dataType", "The current executable profile supports Boolean and number interfaces.");
            }

            if (entry.DataType != DataType.Number && !string.IsNullOrEmpty(entry.Units))
            {
                throw Failure("incompatible_units", $"{path}/{index}/units", "Only number interfaces may declare units.");
            }

            if (entry.DefaultValue is { } value && !DefaultMatches(value, entry.DataType))
            {
                throw Failure("invalid_interface_default", $"{path}/{index}/defaultValue", "Default value does not match the interface type.");
            }
        }
    }

    private static bool DefaultMatches(JsonElement value, DataType type) => type switch
    {
        DataType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        DataType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number),
        _ => false
    };

    private static InterfaceRecord InterfaceEntry(ExecutableFlowSource source, ExecutableFlowNode node)
    {
        var id = node.Configuration["interfaceId"].GetString();
        var entry = node.Kind == "flowInput"
            ? source.Interface.Inputs.Where(item => item.Id == id).Select(item => new InterfaceRecord(item.Id, item.Name, item.DataType, item.Units, item.DefaultValue)).SingleOrDefault()
            : source.Interface.Outputs.Where(item => item.Id == id).Select(item => new InterfaceRecord(item.Id, item.Name, item.DataType, item.Units, null)).SingleOrDefault();
        return entry ?? throw Failure("missing_interface_reference", $"/nodes/{Escape(node.Id)}/configuration/interfaceId", "Referenced interface entry does not exist in the required direction.");
    }

    private static DataType InterfaceDataType(ExecutableFlowSource source, ExecutableFlowNode node) => InterfaceEntry(source, node).DataType switch
    {
        DataType.Boolean => DataType.Boolean,
        DataType.Number => DataType.Number,
        _ => throw new UnreachableException()
    };

    private static string? InterfaceUnits(ExecutableFlowSource source, ExecutableFlowNode node) => InterfaceEntry(source, node).Units;

    private static void ValidateConfiguration(ExecutableFlowSource source, ExecutableFlowNode node, int index)
    {
        var path = $"/nodes/{index}/configuration";
        if (node.Kind is "flowInput" or "flowOutput")
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("interfaceId", out var reference)
                || reference.ValueKind != JsonValueKind.String
                || reference.GetString() is not string interfaceId)
            {
                throw Failure("missing_interface_reference", $"{path}/interfaceId", "An interfaceId string is required.");
            }

            ValidateIdentifier(interfaceId, $"{path}/interfaceId", 63);
            _ = InterfaceEntry(source, node);
        }
        else if (node.Kind is "digitalInput" or "digitalOutput" or "analogInput" or "analogOutput")
        {
            if (!node.Configuration.TryGetValue("pointId", out var point)
                || point.ValueKind != JsonValueKind.String
                || point.GetString() is not string pointId)
            {
                throw Failure("invalid_configuration", path, "A pointId string is required.");
            }

            if (node.Configuration.Keys.Any(key => key is not ("pointId" or "units")))
            {
                throw Failure("invalid_configuration", path, "Only pointId and optional units are supported.");
            }

            if (node.Configuration.TryGetValue("units", out var units) &&
                units.ValueKind != JsonValueKind.String)
            {
                throw Failure(
                    "invalid_configuration",
                    $"{path}/units",
                    "Units must be a string.");
            }

            const int MaxIdentifierBytes = 63;
            ValidateIdentifier(pointId, $"{path}/pointId", MaxIdentifierBytes);
        }
        else if (node.Kind == "digitalConstant")
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("value", out var value)
                || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Failure("invalid_configuration", path, "A Boolean value is required.");
            }
        }
        else if (node.Kind is "numericConstant" or "memory")
        {
            ValidateFiniteNumber(node, path, "value");
        }
        else if (node.Kind == "comparator")
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("operator", out var comparison)
                || comparison.ValueKind != JsonValueKind.String
                || comparison.GetString() is not ("lt" or "lte" or "eq" or "gte" or "gt" or "ne"))
            {
                throw Failure("invalid_configuration", path, "A supported comparison operator is required.");
            }
        }
        else if (node.Kind is "levelShifter" or "line")
        {
            if (node.Configuration.Count != 2)
            {
                throw Failure("invalid_configuration", path, "Finite gain and offset values are required.");
            }

            ValidateFiniteNumber(node, path, "gain");
            ValidateFiniteNumber(node, path, "offset");
        }
        else if (node.Kind is "onDelay" or "delay" or "timer")
        {
            ValidateFiniteNumber(node, path, "durationMs");
            var duration = node.Configuration["durationMs"].GetDouble();
            if (node.Configuration.Count != 1 || duration < 0D || duration > uint.MaxValue)
            {
                throw Failure("invalid_configuration", path, "Timer duration must be from 0 through 4294967295 milliseconds.");
            }
        }
        else if (node.Kind == "clamp")
        {
            if (node.Configuration.Count != 2)
            {
                throw Failure("invalid_configuration", path, "Finite minimum and maximum values are required.");
            }

            ValidateFiniteNumber(node, path, "minimum");
            ValidateFiniteNumber(node, path, "maximum");
            if (node.Configuration["minimum"].GetDouble() > node.Configuration["maximum"].GetDouble())
            {
                throw Failure("invalid_configuration", path, "Minimum must not exceed maximum.");
            }
        }
        else if (node.Kind is "schedule" or "calendar")
        {
            if (node.Configuration.Count != 1 || !node.Configuration.TryGetValue("enabled", out var enabled) ||
                enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Failure("invalid_configuration", path, "An enabled Boolean is required.");
            }
        }
        else if (node.Configuration.Count != 0)
        {
            throw Failure("invalid_configuration", path, "This node requires empty configuration.");
        }
    }

    private static void ValidateFiniteNumber(ExecutableFlowNode node, string path, string key)
    {
        if (!node.Configuration.TryGetValue(key, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var number)
            || !double.IsFinite(number))
        {
            throw Failure("invalid_configuration", $"{path}/{key}", "A finite number is required.");
        }
    }

    private static IEnumerable<ConstantRecord> ConstantsFor(ExecutableFlowNode node)
    {
        if (node.Kind == "digitalConstant")
        {
            yield return GetBooleanConstant(node.Configuration["value"].GetBoolean());
        }
        else if (node.Kind is "numericConstant" or "memory")
        {
            yield return GetNumericConstant(node, "value");
        }
        else if (node.Kind is "levelShifter" or "line")
        {
            yield return GetNumericConstant(node, "gain");
            yield return GetNumericConstant(node, "offset");
        }
        else if (node.Kind == "clamp")
        {
            yield return GetNumericConstant(node, "minimum");
            yield return GetNumericConstant(node, "maximum");
        }
        else if (node.Kind is "onDelay" or "delay" or "timer")
        {
            yield return GetNumericConstant(node, "durationMs");
        }
        else if (node.Kind is "risingEdge" or "pulse")
        {
            yield return GetBooleanConstant(false);
        }
        else if (node.Kind is "schedule" or "calendar")
        {
            yield return GetBooleanConstant(node.Configuration["enabled"].GetBoolean());
        }
    }

    private static ConstantRecord GetBooleanConstant(bool value) => new(DataType.Boolean, value ? 1D : 0D);

    private static ConstantRecord GetNumericConstant(ExecutableFlowNode node, string key) =>
        new(DataType.Number, node.Configuration[key].GetDouble());

    private static ushort ConstantIndex(ConstantRecord[] constants, ConstantRecord value) =>
        checked((ushort)Array.IndexOf(constants, value));

    private static byte[] EncodeConstant(ConstantRecord constant)
    {
        if (constant.DataType == DataType.Boolean)
        {
            return [1, constant.Number != 0D ? (byte)1 : (byte)0, 0, 0];
        }

        var result = new byte[12];
        result[0] = 2;
        BinaryPrimitives.WriteInt64LittleEndian(result.AsSpan(4), BitConverter.DoubleToInt64Bits(constant.Number));
        return result;
    }

    private static DataType ResultDataType(ExecutableFlowSource source, ExecutableFlowNode node) =>
        node.Kind is "flowInput" or "flowOutput" ? InterfaceDataType(source, node)
        : node.Kind is "numericConstant" or "add" or "levelShifter" or "analogInput" or "analogOutput" or
            "average" or "calculator" or "clamp" or "min" or "max" or "line" or "selector" ? DataType.Number : DataType.Boolean;

    private static ushort ComparatorCode(ExecutableFlowNode node) => node.Configuration["operator"].GetString() switch
    {
        "lt" => 1,
        "lte" => 2,
        "eq" => 3,
        "gte" => 4,
        "gt" => 5,
        "ne" => 6,
        _ => throw new UnreachableException()
    };

    private static FlowPort FindPort(
        Dictionary<string, ExecutableFlowNode> nodes,
        Dictionary<string, IReadOnlyDictionary<string, FlowPort>> shapes,
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
        foreach (var node in nodes.Where(node => node.Kind is "digitalOutput" or "analogOutput"))
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

    private static PointRecord[] BuildPoints(
        ExecutableFlowSource source,
        IReadOnlyList<ExecutableFlowNode> nodes,
        IReadOnlyList<Point> resolvedPoints) =>
    [
        .. nodes
            .Where(node => node.Kind is
                "digitalInput" or
                "digitalOutput" or
                "analogInput" or
                "analogOutput" or
                "flowInput" or
                "flowOutput")
            .Select(node => new PointRecord(
                // Id
                node.Configuration[
                    node.Kind is "flowInput" or "flowOutput"
                        ? "interfaceId"
                        : "pointId"].GetString()!,

                // Direction
                checked(node.Kind.EndsWith("Input", StringComparison.Ordinal)
                    ? DataDirection.Input
                    : DataDirection.Output),

                // Type
                node.Kind is "flowInput" or "flowOutput"
                    ? InterfaceDataType(source, node)
                    : node.Kind.StartsWith("analog", StringComparison.Ordinal)
                        ? DataType.Number
                        : DataType.Boolean,

                // Units
                node.Kind is "flowInput" or "flowOutput"
                    ? InterfaceUnits(source, node)
                    : PointUnits(node, resolvedPoints),

                // Kind
                checked((byte)(node.Kind is "flowInput" or "flowOutput" ? 1 : 0))))
            .Distinct()
            .OrderBy(point => point.Kind)
            .ThenBy(point => point.Id, StringComparer.Ordinal)
            .ThenBy(point => point.Direction)
    ];

    /// <summary>
    /// Gets units preserved in the source node when present, otherwise resolves
    /// them from the target point.
    /// </summary>
    private static string? PointUnits(
        ExecutableFlowNode node,
        IReadOnlyList<Point> resolvedPoints)
    {
        if (node.Configuration.TryGetValue("units", out var units))
        {
            return units.GetString();
        }

        return resolvedPoints
            .SingleOrDefault(point =>
                point.Id == node.Configuration["pointId"].GetString())
            ?.Units;
    }

    private static byte[] String8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat([checked((byte)bytes.Length)], bytes);
    }

    private static byte[] String8AllowEmpty(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat([checked((byte)bytes.Length)], bytes);
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

    private static void ValidateUnits(FlowCompilationRequest request)
    {
        var source = request.Source;
        var nodes = source.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var units = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var id in GetSchedule(source))
        {
            var node = nodes[id];

            var value = node.Kind switch
            {
                "analogInput" => request.Target.Points.SingleOrDefault(point => point.Id == node.Configuration["pointId"].GetString())?.Units,
                "flowInput" => InterfaceUnits(source, node),
                "numericConstant" => null,
                "add" => RequireMatchingUnits(source, units, id, "a", "b"),
                "comparator" => RequireMatchingUnits(source, units, id, "a", "b"),
                "levelShifter" => units[SourceNode(source, id, "in")],
                "average" or "calculator" or "clamp" or "line" => units[SourceNode(source, id, "input")],
                "min" or "max" or "selector" => RequireMatchingUnits(source, units, id, "a", "b"),
                _ => null
            };

            units[id] = value;

            if (node.Kind == "analogOutput")
            {
                var inputUnits = units[SourceNode(source, id, "in")];

                var pointUnits = request.Target.Points.SingleOrDefault(point => point.Id == node.Configuration["pointId"].GetString())?.Units;

                if (!string.Equals(inputUnits, pointUnits, StringComparison.Ordinal))
                {
                    throw Failure("unit_mismatch", $"/nodes/{Escape(id)}", "Analog output units do not match its point binding.");
                }
            }

            if (node.Kind == "flowOutput")
            {
                var inputUnits = units[SourceNode(source, id, "value")];
                if (!string.Equals(inputUnits, InterfaceUnits(source, node), StringComparison.Ordinal))
                {
                    throw Failure("unit_mismatch", $"/nodes/{Escape(id)}/ports/value", "Flow output units do not match its input.");
                }
            }
        }
    }

    private static string? RequireMatchingUnits(
        ExecutableFlowSource source,
        Dictionary<string, string?> units,
        string nodeId,
        string leftPort,
        string rightPort)
    {
        var left = units[SourceNode(source, nodeId, leftPort)];
        var right = units[SourceNode(source, nodeId, rightPort)];
        if (!string.Equals(left, right, StringComparison.Ordinal))
        {
            throw Failure("unit_mismatch", $"/nodes/{Escape(nodeId)}", "Numeric operands require identical units.");
        }

        return left;
    }

    private static string SourceNode(ExecutableFlowSource source, string nodeId, string portId) =>
        source.Connections.Single(connection =>
            connection.Target.NodeId == nodeId && connection.Target.PortId == portId).Source.NodeId;

    private static byte[] F64(double value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
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

    /// <summary>
    /// Generates a human-friendly label from <c>node.Kind</c> when <c>node.Label</c> is blank.
    /// <para>
    /// Examples:
    /// <c>digitalInput</c> → <c>Digital Input</c>,
    /// <c>analogOutput</c> → <c>Analog Output</c>,
    /// <c>onDelay</c> → <c>On Delay</c>,
    /// <c>qualityGood</c> → <c>Quality Good</c>.
    /// </para>
    /// </summary>
    private static string AuthoringLabel(ExecutableFlowNode node) => string.IsNullOrWhiteSpace(node.Label)
        ? CamelCaseBoundaryRegex().Replace(node.Kind, " $1").Trim() switch
        {
            var value when value.Length > 0 => char.ToUpperInvariant(value[0]) + value[1..],
            _ => node.Kind
        }
        : node.Label;

    [GeneratedRegex("([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex CamelCaseBoundaryRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    private sealed record FlowPorts(IReadOnlyList<FlowPort> Ports);

    private sealed record FlowPort(string Id, DataDirection Direction, DataType DataType);

    private sealed record FlowPortKey(string NodeId, string PortId);

    private sealed record PointRecord(string Id, DataDirection Direction, DataType DataType, string? Units, byte Kind = 0);

    private sealed record InterfaceRecord(string Id, string Name, DataType DataType, string? Units, JsonElement? DefaultValue);

    private sealed record ConstantRecord(DataType DataType, double Number);

    private sealed record V1Instruction(
        byte Opcode,
        ushort Result,
        ushort Operand0,
        ushort Operand1,
        ushort Auxiliary,
        string NodeId,
        byte Discriminator);

    private sealed record V1Section(ushort Id, uint Count, byte[] Bytes, ushort Version = 1);
}