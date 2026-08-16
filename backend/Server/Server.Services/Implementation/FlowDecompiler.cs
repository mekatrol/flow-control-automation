using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Server.Services.Implementation;

public sealed class FlowDecompiler : IFlowDecompiler
{
    private const int EnvelopeBytes = 128;
    private const int DirectoryEntryBytes = 48;
    private const ushort SectionCount = 8;
    private const ushort Unused = ushort.MaxValue;

    public FlowDecompilationResult Decompile(ReadOnlyMemory<byte> artifact, string? name = null)
    {
        var bytes = artifact.Span;
        var sections = ValidateEnvelope(bytes);
        var constants = ReadConstants(Section(bytes, sections, 1));
        var points = ReadPoints(Section(bytes, sections, 2));
        var slots = ReadSlots(Section(bytes, sections, 3));
        var instructions = ReadInstructions(Section(bytes, sections, 4));
        ValidateCommitPlan(Section(bytes, sections, 5));
        var symbols = ReadSymbols(Section(bytes, sections, 6), instructions.Count);
        ValidateDebugMap(Section(bytes, sections, 7));
        var dependencies = ReadDependencies(Section(bytes, sections, 8));

        var flowId = ReadFlowId(bytes);
        var nodes = new List<FlowNode>();
        var connections = new List<PendingConnection>();
        var slotOwners = new Dictionary<ushort, FlowEndpoint>();
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new Dictionary<int, int>();

        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            var symbol = symbols[index];

            if (instruction.Opcode == FlowOpcode.Commit)
            {
                if (index != instructions.Count - 1 || symbol.NodeId.Length != 0)
                {
                    Fail("invalid_instruction", $"/instructions/{index}", "Commit must be the final anonymous instruction.");
                }

                continue;
            }

            if (instruction.Opcode == FlowOpcode.MemoryCommit)
            {
                RequireSymbol(symbol, index, 1);
                AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "in", index);
                continue;
            }

            RequireSymbol(symbol, index, 0);
            var configuration = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            var kind = instruction.Opcode switch
            {
                FlowOpcode.PointInput => ConfigurePoint(configuration, points, instruction.Auxiliary, DataDirection.Input, index),
                FlowOpcode.DigitalConstant => ConfigureBoolean("digitalConstant", configuration, constants, instruction.Auxiliary, index),
                FlowOpcode.Not => "not",
                FlowOpcode.And => "and",
                FlowOpcode.Or => "or",
                FlowOpcode.Memory => ConfigureState(configuration, slots, constants, instruction.Auxiliary, index),
                FlowOpcode.PointOutput => ConfigurePoint(configuration, points, instruction.Auxiliary, DataDirection.Output, index),
                FlowOpcode.Nand => "nand",
                FlowOpcode.Nor => "nor",
                FlowOpcode.Xor => "xor",
                FlowOpcode.Xnor => "xnor",
                FlowOpcode.NumericConstant => ConfigureNumber("numericConstant", configuration, constants, instruction.Auxiliary, index),
                FlowOpcode.Add => "add",
                FlowOpcode.Comparator => ConfigureComparator(configuration, instruction.Auxiliary, index),
                FlowOpcode.LevelShifter => ConfigureLevelShifter(configuration, constants, instruction.Operand1, instruction.Auxiliary, index),
                FlowOpcode.QualityGood => "qualityGood",
                FlowOpcode.OnDelay => ConfigureTimer(configuration, slots, constants, instruction.Auxiliary, index),
                FlowOpcode.RisingEdge => "risingEdge",
                _ => throw Error("unsupported_opcode", $"/instructions/{index}/opcode", $"Opcode {instruction.Opcode} cannot be represented by the designer.")
            };

            var inputs = new List<ushort>();

            switch (instruction.Opcode)
            {
                case FlowOpcode.Not:
                    AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "in", index);
                    inputs.Add(instruction.Operand0);
                    break;
                case FlowOpcode.And:
                case FlowOpcode.Or:
                case FlowOpcode.Nand:
                case FlowOpcode.Nor:
                case FlowOpcode.Xor:
                case FlowOpcode.Xnor:
                    AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "a", index);
                    AddConnection(connections, slotOwners, instruction.Operand1, symbol.NodeId, "b", index);
                    inputs.Add(instruction.Operand0);
                    inputs.Add(instruction.Operand1);
                    break;
                case FlowOpcode.Add:
                case FlowOpcode.Comparator:
                    AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "a", index);
                    AddConnection(connections, slotOwners, instruction.Operand1, symbol.NodeId, "b", index);
                    inputs.Add(instruction.Operand0);
                    inputs.Add(instruction.Operand1);
                    break;
                case FlowOpcode.LevelShifter:
                case FlowOpcode.QualityGood:
                case FlowOpcode.OnDelay:
                case FlowOpcode.RisingEdge:
                    AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "in", index);
                    inputs.Add(instruction.Operand0);
                    break;
                case FlowOpcode.PointOutput:
                    AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "in", index);
                    inputs.Add(instruction.Operand0);
                    break;
            }

            var depth = inputs.Count == 0 ? 0 : inputs.Max(slot => depths[slotOwners[slot].NodeId]) + 1;
            var row = rows.GetValueOrDefault(depth);

            rows[depth] = row + 1;

            nodes.Add(new FlowNode
            {
                Id = symbol.NodeId,
                Kind = kind,
                Label = symbol.Label,
                X = symbol.X,
                Y = symbol.Y,
                ZOrder = symbol.ZOrder,
                GroupId = symbol.GroupId.Length == 0 ? null : symbol.GroupId,
                Connectors = Connectors(kind),
                Configuration = configuration
            });

            depths[symbol.NodeId] = depth;

            if (instruction.Result == Unused || slotOwners.ContainsKey(instruction.Result))
            {
                Fail("invalid_operand", $"/instructions/{index}/result", "A node result must write one unique slot.");
            }

            slotOwners[instruction.Result] = new FlowEndpoint(symbol.NodeId, "value");
        }

        var templates = dependencies.Where(item => item.Kind == 1).ToArray();

        if (templates.Length != 1)
        {
            throw Error("invalid_dependency", "/dependencies/template", "Exactly one controller-template dependency is required.");
        }

        var template = templates[0];

        var flow = new Flow
        {
            Id = flowId,
            Name = string.IsNullOrWhiteSpace(name) ? Label(flowId) : name.Trim(),
            Description = $"Recovered from Flow IL v1 revision {U32(bytes, 16)}.",
            UpdatedAt = "1970-01-01T00:00:00Z",
            Nodes = nodes,
            Connections = [.. connections.Select((item, index) =>
                new FlowConnection(
                    $"connection-{index + 1:D3}",
                    item.Source,
                    new FlowEndpoint(item.TargetNodeId, item.TargetPortId)))
            ]
        };

        FlowValidator.Validate(flow);

        return new FlowDecompilationResult
        {
            Flow = flow,
            RecoveryLevel = "lossless",
            Warnings = [],
            Provenance = new FlowDecompilationProvenance(
                1,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                U32(bytes, 16),
                template.Id,
                template.Revision)
        };
    }

    private static SectionInfo[] ValidateEnvelope(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < EnvelopeBytes || bytes.Length > 16384 || !bytes[..4].SequenceEqual("FIL1"u8))
        {
            Fail("malformed_artifact", "/", "The artifact is not a bounded Flow IL v1 envelope.");
        }

        if (U16(bytes, 4) != 1)
        {
            Fail("unsupported_version", "/version", "Only Flow IL v1 can be decompiled.");
        }

        if (U16(bytes, 6) != EnvelopeBytes || U32(bytes, 8) != bytes.Length || U16(bytes, 26) != SectionCount || U32(bytes, 116) != EnvelopeBytes)
        {
            Fail("malformed_artifact", "/envelope", "Envelope lengths or section count are invalid.");
        }

        var result = new SectionInfo[SectionCount];
        var expectedOffset = EnvelopeBytes + (SectionCount * DirectoryEntryBytes);
        for (var index = 0; index < SectionCount; index++)
        {
            var entry = bytes.Slice(EnvelopeBytes + (index * DirectoryEntryBytes), DirectoryEntryBytes);
            var id = U16(entry, 0);
            var offset = checked((int)U32(entry, 4));
            var length = checked((int)U32(entry, 8));
            var count = checked((int)U32(entry, 12));
            var version = U16(entry, 2);
            if (id != index + 1 || version != 1)
            {
                Fail("invalid_section", $"/sections/{index}", "Sections must use canonical IDs, order, and version.");
            }

            if (offset != expectedOffset || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            {
                Fail("invalid_section", $"/sections/{index}", "Section bounds are invalid.");
            }

            if (!SHA256.HashData(bytes.Slice(offset, length)).AsSpan().SequenceEqual(entry.Slice(16, 32)))
            {
                Fail("invalid_digest", $"/sections/{index}/digest", "Section digest does not match its contents.");
            }

            result[index] = new SectionInfo(offset, length, count, version);
            expectedOffset = checked(offset + length);
        }

        if (expectedOffset != bytes.Length)
        {
            Fail("malformed_artifact", "/artifactLength", "The final section must end at artifact length.");
        }

        return result;
    }

    private static List<ConstantRecord> ReadConstants(SectionReader reader)
    {
        var values = new List<ConstantRecord>();
        for (var i = 0; i < reader.Count; i++)
        {
            var prefix = reader.Fixed(4, $"/constants/{i}");

            // Boolean value?
            if (prefix[0] == (byte)DataType.Boolean && prefix[1] <= 1 && U16(prefix, 2) == 0)
            {
                values.Add(new ConstantRecord(DataType.Boolean, prefix[1]));
            }

            // Number value?
            else if (prefix[0] == (byte)DataType.Number && prefix[1] == 0 && U16(prefix, 2) == 0)
            {
                var bits = BinaryPrimitives.ReadInt64LittleEndian(reader.Fixed(8, $"/constants/{i}/value"));

                var number = BitConverter.Int64BitsToDouble(bits);

                if (!double.IsFinite(number))
                {
                    Fail("invalid_constant", $"/constants/{i}", "Numeric constants must be finite.");
                }

                values.Add(new ConstantRecord(DataType.Number, number));
            }

            // Data type is unsupported
            else
            {
                Fail("invalid_constant", $"/constants/{i}", "Constant encoding is unsupported.");
            }
        }
        reader.End("/constants");
        return values;
    }

    private static List<PointRecord> ReadPoints(SectionReader reader)
    {
        var values = new List<PointRecord>();

        for (var i = 0; i < reader.Count; i++)
        {
            var prefix = reader.Fixed(4, $"/points/{i}");
            var id = reader.String8($"/points/{i}/id");
            var units = reader.String8AllowEmpty($"/points/{i}/units");

            if (prefix[0] is not ((byte)DataType.Boolean or (byte)DataType.Number) || prefix[1] is not (1 or 2))
            {
                Fail("unsupported_point", $"/points/{i}", "Point binding type is unsupported.");
            }

            values.Add(new PointRecord((DataDirection)prefix[0], (DataType)prefix[1], id, units));
        }

        reader.End("/points");
        return values;
    }

    private static IReadOnlyDictionary<ushort, SlotRecord> ReadSlots(SectionReader reader)
    {
        var values = new Dictionary<ushort, SlotRecord>();
        for (var i = 0; i < reader.Count; i++)
        {
            var record = reader.Fixed(8, $"/slots/{i}");

            var slot = new SlotRecord(
                (FlowSlotKind)record[0],
                record[1],
                U16(record, 6));

            if (record[1] is not (1 or 2) || !values.TryAdd(U16(record, 4), slot))
            {
                Fail("invalid_slot", $"/slots/{i}", "Slot is unsupported or duplicated.");
            }
        }
        reader.End("/slots");
        return values;
    }

    private static IReadOnlyList<Instruction> ReadInstructions(SectionReader reader)
    {
        var values = new List<Instruction>();
        for (var i = 0; i < reader.Count; i++)
        {
            var record = reader.Fixed(12, $"/instructions/{i}");
            if (record[1] != 0 || U16(record, 10) != 0)
            {
                Fail("invalid_instruction", $"/instructions/{i}", "Instruction flags and reserved fields must be zero.");
            }

            values.Add(new Instruction(
                (FlowOpcode)record[0],
                U16(record, 2),
                U16(record, 4),
                U16(record, 6),
                U16(record, 8)));
        }
        reader.End("/instructions");
        return values;
    }

    private static IReadOnlyList<SymbolRecord> ReadSymbols(SectionReader reader, int instructionCount)
    {
        if (reader.Count != instructionCount)
        {
            Fail("invalid_symbols", "/symbols", "Every instruction requires one symbol record.");
        }

        var values = new List<SymbolRecord>();
        for (var i = 0; i < reader.Count; i++)
        {
            var prefix = reader.Fixed(3, $"/symbols/{i}");
            if (U16(prefix, 0) != i)
            {
                Fail("invalid_symbols", $"/symbols/{i}", "Symbol indices must be canonical.");
            }

            var nodeId = reader.String8AllowEmpty($"/symbols/{i}/nodeId");
            var label = reader.String8AllowEmpty($"/symbols/{i}/label");
            var x = reader.F64($"/symbols/{i}/x");
            var y = reader.F64($"/symbols/{i}/y");
            var zOrder = reader.F64($"/symbols/{i}/zOrder");
            var groupId = reader.String8AllowEmpty($"/symbols/{i}/groupId");
            values.Add(new SymbolRecord(prefix[2], nodeId, label, x, y, zOrder, groupId));
        }
        reader.End("/symbols");
        return values;
    }

    private static IReadOnlyList<Dependency> ReadDependencies(SectionReader reader)
    {
        var values = new List<Dependency>();
        for (var i = 0; i < reader.Count; i++)
        {
            var kind = reader.Fixed(1, $"/dependencies/{i}")[0];
            var id = reader.String8($"/dependencies/{i}/id");
            var revision = U32(reader.Fixed(4, $"/dependencies/{i}/revision"), 0);
            if (revision == 0)
            {
                Fail("invalid_dependency", $"/dependencies/{i}/revision", "Dependency revision must be positive.");
            }

            values.Add(new Dependency(kind, id, revision));
        }
        reader.End("/dependencies");
        return values;
    }

    private static void ValidateCommitPlan(SectionReader reader)
    {
        for (var i = 0; i < reader.Count; i++)
        {
            _ = reader.Fixed(8, $"/commit/{i}");
        }

        reader.End("/commit");
    }

    private static void ValidateDebugMap(SectionReader reader)
    {
        for (var i = 0; i < reader.Count; i++)
        {
            _ = reader.Fixed(4, $"/debugMap/{i}"); _ = reader.String8($"/debugMap/{i}/nodeId");
        }

        reader.End("/debugMap");
    }

    private static string ConfigurePoint(
        Dictionary<string, JsonElement> config,
        IReadOnlyList<PointRecord> points,
        ushort index,
        DataDirection direction,
        int instruction)
    {
        if (index >= points.Count || points[index].Direction != direction)
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instruction}/auxiliary",
                "Point binding is missing or has the wrong direction.");
        }

        var point = points[index];

        config["pointId"] = JsonSerializer.SerializeToElement(point.Id);
        config["units"] = JsonSerializer.SerializeToElement(point.Units);

        return point.DataType == DataType.Number
            ? direction == DataDirection.Input ? "analogInput" : "analogOutput"
            : direction == DataDirection.Input ? "digitalInput" : "digitalOutput";
    }

    private static string ConfigureBoolean(string kind, Dictionary<string, JsonElement> config, IReadOnlyList<ConstantRecord> constants, ushort index, int instruction)
    {
        if (index >= constants.Count || constants[index].DataType != DataType.Boolean)
        {
            Fail("invalid_operand", $"/instructions/{instruction}/auxiliary", "Boolean constant index is out of range.");
        }

        config["value"] = JsonSerializer.SerializeToElement(constants[index].Number != 0D);

        return kind;
    }

    private static string ConfigureState(Dictionary<string, JsonElement> config, IReadOnlyDictionary<ushort, SlotRecord> slots, IReadOnlyList<ConstantRecord> constants, ushort index, int instruction)
    {
        if (!slots.TryGetValue(index, out var slot) || slot.Kind != FlowSlotKind.MemoryState || slot.InitialConstant >= constants.Count || constants[slot.InitialConstant].DataType != DataType.Number)
        {
            throw Error("invalid_operand", $"/instructions/{instruction}/auxiliary", "State slot is invalid.");
        }

        config["value"] = JsonSerializer.SerializeToElement(constants[slot.InitialConstant].Number);
        return "memory";
    }

    private static string ConfigureNumber(string kind, Dictionary<string, JsonElement> config, IReadOnlyList<ConstantRecord> constants, ushort index, int instruction)
    {
        if (index >= constants.Count || constants[index].DataType != DataType.Number)
        {
            Fail("invalid_operand", $"/instructions/{instruction}/auxiliary", "Numeric constant index is out of range.");
        }

        config["value"] = JsonSerializer.SerializeToElement(constants[index].Number);
        return kind;
    }

    private static string ConfigureComparator(Dictionary<string, JsonElement> config, ushort code, int instruction)
    {
        var value = code switch { 1 => "lt", 2 => "lte", 3 => "eq", 4 => "gte", 5 => "gt", 6 => "ne", _ => null };
        if (value is null)
        {
            Fail("invalid_operand", $"/instructions/{instruction}/auxiliary", "Comparison operator is invalid.");
        }

        config["operator"] = JsonSerializer.SerializeToElement(value);
        return "comparator";
    }

    private static string ConfigureLevelShifter(Dictionary<string, JsonElement> config, IReadOnlyList<ConstantRecord> constants, ushort gain, ushort offset, int instruction)
    {
        if (gain >= constants.Count || offset >= constants.Count || constants[gain].DataType != DataType.Number || constants[offset].DataType != DataType.Number)
        {
            Fail("invalid_operand", $"/instructions/{instruction}", "Level-shifter constants are invalid.");
        }

        config["gain"] = JsonSerializer.SerializeToElement(constants[gain].Number);
        config["offset"] = JsonSerializer.SerializeToElement(constants[offset].Number);
        return "levelShifter";
    }

    private static string ConfigureTimer(Dictionary<string, JsonElement> config, IReadOnlyDictionary<ushort, SlotRecord> slots, IReadOnlyList<ConstantRecord> constants, ushort state, int instruction)
    {
        if (!slots.TryGetValue(state, out var slot) || slot.Kind != FlowSlotKind.TimerState || slot.InitialConstant >= constants.Count || constants[slot.InitialConstant].DataType != DataType.Number)
        {
            Fail("invalid_operand", $"/instructions/{instruction}/timer", "Timer state is invalid.");
        }

        var timer = slot!;
        config["durationMs"] = JsonSerializer.SerializeToElement(constants[timer.InitialConstant].Number);
        return "onDelay";
    }

    private static void AddConnection(List<PendingConnection> result, IReadOnlyDictionary<ushort, FlowEndpoint> owners, ushort slot, string target, string port, int instruction)
    {
        if (!owners.TryGetValue(slot, out var source))
        {
            throw Error("invalid_operand", $"/instructions/{instruction}", "An input does not reference an earlier node result.");
        }

        result.Add(new PendingConnection(source, target, port));
    }

    private static IReadOnlyList<FlowConnector> Connectors(string kind) => kind switch
    {
        "digitalInput" or "digitalConstant" => [BooleanOutput("value", "Value")],
        "analogInput" => [NumberOutput("value", "Value")],
        "digitalOutput" => [BooleanInput("in", "Input")],
        "analogOutput" => [NumberInput("in", "Input")],
        "not" => [BooleanInput("in", "Input"), BooleanOutput("value", "Value")],
        "and" or "or" or "nand" or "nor" or "xor" or "xnor" => [BooleanInput("a", "A"), BooleanInput("b", "B"), BooleanOutput("value", "Value")],
        "memory" => [NumberInput("in", "Input"), NumberOutput("value", "Previous value")],
        "numericConstant" => [NumberOutput("value", "Value")],
        "add" => [NumberInput("a", "A"), NumberInput("b", "B"), NumberOutput("value", "Value")],
        "comparator" => [NumberInput("a", "A"), NumberInput("b", "B"), BooleanOutput("value", "Value")],
        "levelShifter" => [NumberInput("in", "Input"), NumberOutput("value", "Value")],
        "qualityGood" or "onDelay" or "risingEdge" => [BooleanInput("in", "Input"), BooleanOutput("value", "Value")],
        _ => []
    };

    private static FlowConnector BooleanInput(string id, string label) => new(id, label, DataDirection.Input, DataType.Boolean, "left");
    private static FlowConnector BooleanOutput(string id, string label) => new(id, label, DataDirection.Output, DataType.Boolean, "right");
    private static FlowConnector NumberInput(string id, string label) => new(id, label, DataDirection.Input, DataType.Number, "left");
    private static FlowConnector NumberOutput(string id, string label) => new(id, label, DataDirection.Output, DataType.Number, "right");
    private static string Label(string value) => string.Join(' ', value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    private static void RequireSymbol(SymbolRecord symbol, int index, byte discriminator)
    {
        if (symbol.NodeId.Length == 0 || symbol.Discriminator != discriminator)
        {
            Fail("invalid_symbols", $"/symbols/{index}", "Instruction symbol cannot be represented as a designer node.");
        }
    }
    private static string ReadFlowId(ReadOnlySpan<byte> bytes) { var length = bytes[52]; if (length is 0 or > 63) { Fail("invalid_identifier", "/flowId", "Flow ID length is invalid."); } return Encoding.UTF8.GetString(bytes.Slice(53, length)); }

    private static SectionReader Section(ReadOnlySpan<byte> artifact, SectionInfo[] sections, int id) { var value = sections[id - 1]; return new SectionReader(artifact.Slice(value.Offset, value.Length).ToArray(), value.Count, value.Version); }

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);

    private static FlowDecompilationException Error(string code, string path, string message) => new(new(code, path, message));

    private static void Fail(string code, string path, string message) => throw Error(code, path, message);

    private sealed class SectionReader(byte[] bytes, int count, ushort version)
    {
        private int _offset;
        public int Count { get; } = count;
        public ushort Version { get; } = version;
        public ReadOnlySpan<byte> Fixed(int length, string path) { if (length < 0 || _offset > bytes.Length - length) { Fail("malformed_section", path, "Section record is truncated."); } var result = bytes.AsSpan(_offset, length); _offset += length; return result; }
        public string String8(string path) { var value = String8AllowEmpty(path); if (value.Length == 0) { Fail("invalid_identifier", path, "Identifier must not be empty."); } return value; }
        public string String8AllowEmpty(string path) { var length = Fixed(1, path)[0]; var value = Encoding.UTF8.GetString(Fixed(length, path)); if (Encoding.UTF8.GetByteCount(value) != length) { Fail("invalid_identifier", path, "Identifier is not canonical UTF-8."); } return value; }
        public void End(string path)
        {
            if (_offset != bytes.Length)
            {
                Fail("malformed_section", path, "Section has trailing bytes.");
            }
        }
        public double F64(string path) { var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(Fixed(8, path))); if (!double.IsFinite(value)) { Fail("invalid_authoring_metadata", path, "Authoring coordinates must be finite."); } return value; }
    }

    private sealed record SectionInfo(int Offset, int Length, int Count, ushort Version);

    private sealed record PointRecord(DataDirection Direction, DataType DataType, string Id, string Units);

    private sealed record ConstantRecord(DataType DataType, double Number);

    private sealed record SlotRecord(FlowSlotKind Kind, byte Type, ushort InitialConstant);

    private sealed record Instruction(FlowOpcode Opcode, ushort Result, ushort Operand0, ushort Operand1, ushort Auxiliary);

    private sealed record SymbolRecord(byte Discriminator, string NodeId, string Label, double X, double Y, double ZOrder, string GroupId);

    private sealed record Dependency(byte Kind, string Id, uint Revision);

    private sealed record PendingConnection(FlowEndpoint Source, string TargetNodeId, string TargetPortId);
}