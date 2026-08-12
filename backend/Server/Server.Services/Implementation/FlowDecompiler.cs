using Server.Services.Contracts;
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
            if (instruction.Opcode == 255)
            {
                if (index != instructions.Count - 1 || symbol.NodeId.Length != 0)
                {
                    Fail("invalid_instruction", $"/instructions/{index}", "Commit must be the final anonymous instruction.");
                }

                continue;
            }

            if (instruction.Opcode == 8)
            {
                RequireSymbol(symbol, index, 1);
                AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "in", index);
                continue;
            }

            RequireSymbol(symbol, index, 0);
            var configuration = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var kind = instruction.Opcode switch
            {
                1 => ConfigurePoint("digitalInput", configuration, points, instruction.Auxiliary, 1, index),
                2 => ConfigureBoolean("digitalConstant", configuration, constants, instruction.Auxiliary, index),
                3 => "not",
                4 => "and",
                5 => "or",
                6 => ConfigureState(configuration, slots, constants, instruction.Auxiliary, index),
                7 => ConfigurePoint("digitalOutput", configuration, points, instruction.Auxiliary, 2, index),
                _ => throw Error("unsupported_opcode", $"/instructions/{index}/opcode", $"Opcode {instruction.Opcode} cannot be represented by the designer.")
            };

            var inputs = new List<ushort>();
            switch (instruction.Opcode)
            {
                case 3:
                    AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "in", index);
                    inputs.Add(instruction.Operand0);
                    break;
                case 4:
                case 5:
                    AddConnection(connections, slotOwners, instruction.Operand0, symbol.NodeId, "a", index);
                    AddConnection(connections, slotOwners, instruction.Operand1, symbol.NodeId, "b", index);
                    inputs.Add(instruction.Operand0);
                    inputs.Add(instruction.Operand1);
                    break;
                case 7:
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
                Label = Label(kind),
                X = depth * 220,
                Y = row * 120,
                ZOrder = nodes.Count,
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
            Description = $"Recovered from Flow IL v2 revision {U32(bytes, 16)}.",
            UpdatedAt = "1970-01-01T00:00:00Z",
            Nodes = nodes,
            Connections = connections.Select((item, index) => new FlowConnection(
                $"connection-{index + 1:D3}", item.Source, new FlowEndpoint(item.TargetNodeId, item.TargetPortId))).ToArray()
        };
        FlowValidator.Validate(flow);

        return new FlowDecompilationResult
        {
            Flow = flow,
            RecoveryLevel = "normalized",
            Warnings = ["Canvas layout and non-runtime labels were not present in this artifact; deterministic replacements were generated."],
            Provenance = new FlowDecompilationProvenance(
                2,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                U32(bytes, 16),
                template.Id,
                template.Revision)
        };
    }

    private static SectionInfo[] ValidateEnvelope(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < EnvelopeBytes || bytes.Length > 8192 || !bytes[..4].SequenceEqual("FIL2"u8))
        {
            Fail("malformed_artifact", "/", "The artifact is not a bounded Flow IL v2 envelope.");
        }

        if (U16(bytes, 4) != 2) Fail("unsupported_version", "/version", "Only Flow IL v2 can be decompiled.");
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
            if (id != index + 1 || U16(entry, 2) != 1) Fail("invalid_section", $"/sections/{index}", "Sections must use canonical IDs, order, and version.");
            if (offset != expectedOffset || length < 0 || offset > bytes.Length || length > bytes.Length - offset) Fail("invalid_section", $"/sections/{index}", "Section bounds are invalid.");
            if (!SHA256.HashData(bytes.Slice(offset, length)).AsSpan().SequenceEqual(entry.Slice(16, 32))) Fail("invalid_digest", $"/sections/{index}/digest", "Section digest does not match its contents.");
            result[index] = new SectionInfo(offset, length, count);
            expectedOffset = checked(offset + length);
        }

        if (expectedOffset != bytes.Length) Fail("malformed_artifact", "/artifactLength", "The final section must end at artifact length.");
        return result;
    }

    private static IReadOnlyList<bool> ReadConstants(SectionReader reader)
    {
        var values = new List<bool>();
        for (var i = 0; i < reader.Count; i++)
        {
            var record = reader.Fixed(4, $"/constants/{i}");
            if (record[0] != 1 || record[1] > 1 || U16(record, 2) != 0) Fail("invalid_constant", $"/constants/{i}", "Only canonical Boolean constants are supported.");
            values.Add(record[1] != 0);
        }
        reader.End("/constants");
        return values;
    }

    private static IReadOnlyList<PointRecord> ReadPoints(SectionReader reader)
    {
        var values = new List<PointRecord>();
        for (var i = 0; i < reader.Count; i++)
        {
            var prefix = reader.Fixed(4, $"/points/{i}");
            var id = reader.String8($"/points/{i}/id");
            if (prefix[0] is not (1 or 2) || prefix[1] != 1) Fail("unsupported_point", $"/points/{i}", "Only Boolean read/write bindings are supported.");
            values.Add(new PointRecord(prefix[0], id));
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
            var slot = new SlotRecord(record[0], record[1], U16(record, 6));
            if (record[1] != 1 || !values.TryAdd(U16(record, 4), slot)) Fail("invalid_slot", $"/slots/{i}", "Slot is unsupported or duplicated.");
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
            if (record[1] != 0 || U16(record, 10) != 0) Fail("invalid_instruction", $"/instructions/{i}", "Instruction flags and reserved fields must be zero.");
            values.Add(new Instruction(record[0], U16(record, 2), U16(record, 4), U16(record, 6), U16(record, 8)));
        }
        reader.End("/instructions");
        return values;
    }

    private static IReadOnlyList<SymbolRecord> ReadSymbols(SectionReader reader, int instructionCount)
    {
        if (reader.Count != instructionCount) Fail("invalid_symbols", "/symbols", "Every instruction requires one symbol record.");
        var values = new List<SymbolRecord>();
        for (var i = 0; i < reader.Count; i++)
        {
            var prefix = reader.Fixed(3, $"/symbols/{i}");
            if (U16(prefix, 0) != i) Fail("invalid_symbols", $"/symbols/{i}", "Symbol indices must be canonical.");
            values.Add(new SymbolRecord(prefix[2], reader.String8AllowEmpty($"/symbols/{i}/nodeId")));
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
            if (revision == 0) Fail("invalid_dependency", $"/dependencies/{i}/revision", "Dependency revision must be positive.");
            values.Add(new Dependency(kind, id, revision));
        }
        reader.End("/dependencies");
        return values;
    }

    private static void ValidateCommitPlan(SectionReader reader) { for (var i = 0; i < reader.Count; i++) _ = reader.Fixed(8, $"/commit/{i}"); reader.End("/commit"); }
    private static void ValidateDebugMap(SectionReader reader) { for (var i = 0; i < reader.Count; i++) { _ = reader.Fixed(4, $"/debugMap/{i}"); _ = reader.String8($"/debugMap/{i}/nodeId"); } reader.End("/debugMap"); }

    private static string ConfigurePoint(string kind, Dictionary<string, JsonElement> config, IReadOnlyList<PointRecord> points, ushort index, byte direction, int instruction)
    {
        if (index >= points.Count || points[index].Direction != direction) Fail("invalid_operand", $"/instructions/{instruction}/auxiliary", "Point binding is missing or has the wrong direction.");
        config["pointId"] = JsonSerializer.SerializeToElement(points[index].Id);
        return kind;
    }

    private static string ConfigureBoolean(string kind, Dictionary<string, JsonElement> config, IReadOnlyList<bool> constants, ushort index, int instruction)
    {
        if (index >= constants.Count) Fail("invalid_operand", $"/instructions/{instruction}/auxiliary", "Constant index is out of range.");
        config["value"] = JsonSerializer.SerializeToElement(constants[index]);
        return kind;
    }

    private static string ConfigureState(Dictionary<string, JsonElement> config, IReadOnlyDictionary<ushort, SlotRecord> slots, IReadOnlyList<bool> constants, ushort index, int instruction)
    {
        if (!slots.TryGetValue(index, out var slot) || slot.Kind != 3 || slot.InitialConstant >= constants.Count) throw Error("invalid_operand", $"/instructions/{instruction}/auxiliary", "State slot is invalid.");
        config["value"] = JsonSerializer.SerializeToElement(constants[slot.InitialConstant]);
        return "memory";
    }

    private static void AddConnection(List<PendingConnection> result, IReadOnlyDictionary<ushort, FlowEndpoint> owners, ushort slot, string target, string port, int instruction)
    {
        if (!owners.TryGetValue(slot, out var source)) throw Error("invalid_operand", $"/instructions/{instruction}", "An input does not reference an earlier node result.");
        result.Add(new PendingConnection(source, target, port));
    }

    private static IReadOnlyList<FlowConnector> Connectors(string kind) => kind switch
    {
        "digitalInput" or "digitalConstant" => [Output("value", "Value")],
        "digitalOutput" => [Input("in", "Input")],
        "not" => [Input("in", "Input"), Output("value", "Value")],
        "and" or "or" => [Input("a", "A"), Input("b", "B"), Output("value", "Value")],
        "memory" => [Input("in", "Input"), Output("value", "Previous value")],
        _ => []
    };

    private static FlowConnector Input(string id, string label) => new(id, label, "input", "boolean", "left");
    private static FlowConnector Output(string id, string label) => new(id, label, "output", "boolean", "right");
    private static string Label(string value) => string.Join(' ', value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    private static void RequireSymbol(SymbolRecord symbol, int index, byte discriminator) { if (symbol.NodeId.Length == 0 || symbol.Discriminator != discriminator) Fail("invalid_symbols", $"/symbols/{index}", "Instruction symbol cannot be represented as a designer node."); }
    private static string ReadFlowId(ReadOnlySpan<byte> bytes) { var length = bytes[52]; if (length is 0 or > 63) Fail("invalid_identifier", "/flowId", "Flow ID length is invalid."); return Encoding.UTF8.GetString(bytes.Slice(53, length)); }
    private static SectionReader Section(ReadOnlySpan<byte> artifact, SectionInfo[] sections, int id) { var value = sections[id - 1]; return new SectionReader(artifact.Slice(value.Offset, value.Length).ToArray(), value.Count); }
    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static FlowDecompilationException Error(string code, string path, string message) => new(new(code, path, message));
    private static void Fail(string code, string path, string message) => throw Error(code, path, message);

    private sealed class SectionReader(byte[] bytes, int count)
    {
        private int _offset;
        public int Count { get; } = count;
        public ReadOnlySpan<byte> Fixed(int length, string path) { if (length < 0 || _offset > bytes.Length - length) Fail("malformed_section", path, "Section record is truncated."); var result = bytes.AsSpan(_offset, length); _offset += length; return result; }
        public string String8(string path) { var value = String8AllowEmpty(path); if (value.Length == 0) Fail("invalid_identifier", path, "Identifier must not be empty."); return value; }
        public string String8AllowEmpty(string path) { var length = Fixed(1, path)[0]; var value = Encoding.UTF8.GetString(Fixed(length, path)); if (Encoding.UTF8.GetByteCount(value) != length) Fail("invalid_identifier", path, "Identifier is not canonical UTF-8."); return value; }
        public void End(string path) { if (_offset != bytes.Length) Fail("malformed_section", path, "Section has trailing bytes."); }
    }

    private sealed record SectionInfo(int Offset, int Length, int Count);
    private sealed record PointRecord(byte Direction, string Id);
    private sealed record SlotRecord(byte Kind, byte Type, ushort InitialConstant);
    private sealed record Instruction(byte Opcode, ushort Result, ushort Operand0, ushort Operand1, ushort Auxiliary);
    private sealed record SymbolRecord(byte Discriminator, string NodeId);
    private sealed record Dependency(byte Kind, string Id, uint Revision);
    private sealed record PendingConnection(FlowEndpoint Source, string TargetNodeId, string TargetPortId);
}
