/*
 * FlowDecompiler
 * ==========================================
 *
 * Think of this class as the byte-level inverse of FlowCompiler:
 *
 *   Flow IL byte[]
 *       -> validate 128-byte envelope
 *       -> validate/read eight 48-byte directory entries
 *       -> verify each section SHA-256
 *       -> decode section records using explicit byte widths
 *       -> decode the scheduled 12-byte instruction stream
 *       -> use symbols to restore authoring identity/layout
 *       -> use result-slot ownership + operands to reconstruct graph edges
 *       -> emit a designer Flow DTO and validate it
 *
 * The parser intentionally does not execute the artifact. It treats the artifact
 * as untrusted bytes and checks bounds before converting fields into C# values.
 */

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

    /*
     * Top-level decode pipeline.
     *
     * No graph reconstruction begins until ValidateEnvelope() has checked the magic,
     * version/length framing, all eight directory ranges, and every section digest.
     * The section readers then decode typed records; only after that do instructions
     * and symbols get translated back into designer nodes/connections.
     */
    public FlowDecompilationResult Decompile(ReadOnlyMemory<byte> artifact, string? name = null)
    {
        /*
         * Keep the public method focused on the high-level inverse pipeline:
         *
         *     artifact bytes
         *          |
         *          v
         *     validated section records
         *          |
         *          v
         *     reconstructed nodes + connections
         *          |
         *          v
         *     validated designer Flow
         */
        var decoded = DecodeArtifact(artifact);
        var graph = ReconstructGraph(decoded);
        var flow = BuildRecoveredFlow(decoded, graph, name);

        FlowValidator.Validate(flow);

        return new FlowDecompilationResult
        {
            Flow = flow,
            RecoveryLevel = "lossless",
            Warnings = [],
            Provenance = new FlowDecompilationProvenance(
                1,
                Convert.ToHexStringLower(SHA256.HashData(decoded.Bytes)),
                U32(decoded.Bytes, 16),
                decoded.Template.Id,
                decoded.Template.Revision)
        };
    }

    /*
     * Validate the artifact framing and decode each binary section into typed records.
     * Graph reconstruction deliberately starts only after this method has established
     * that the envelope, section ranges, hashes and basic record shapes are valid.
     */
    private static DecodedArtifact DecodeArtifact(ReadOnlyMemory<byte> artifact)
    {
        // Span gives cheap, bounds-aware views over the caller's immutable artifact.
        var bytes = artifact.Span;

        // Parse the fixed 128-byte header and the 8 x 48-byte directory. The returned
        // SectionInfo values are trusted only because ValidateEnvelope checks them.
        var sections = ValidateEnvelope(bytes);

        // Decode sections in canonical ID order. Section() copies exactly the
        // directory-declared payload range into a bounded SectionReader.
        var constants = ReadConstants(Section(bytes, sections, 1));
        var points = ReadPoints(Section(bytes, sections, 2));
        var slots = ReadSlots(Section(bytes, sections, 3));
        var instructions = ReadInstructions(Section(bytes, sections, 4));
        ValidateCommitPlan(Section(bytes, sections, 5));
        var symbols = ReadSymbols(Section(bytes, sections, 6), instructions.Count);
        ValidateDebugMap(Section(bytes, sections, 7));
        var dependencies = ReadDependencies(Section(bytes, sections, 8));

        var templates = dependencies.Where(item => item.Kind == 1).ToArray();

        if (templates.Length != 1)
        {
            throw Error(
                "invalid_dependency",
                "/dependencies/template",
                "Exactly one controller-template dependency is required.");
        }

        return new DecodedArtifact(
            bytes.ToArray(),
            ReadFlowId(bytes),
            constants,
            points,
            slots,
            instructions,
            symbols,
            dependencies,
            templates[0]);
    }

    /*
     * Reconstruct the designer graph topology from the scheduled instruction stream.
     *
     * The binary has no designer "edge table". Instead, a producing instruction
     * writes a result slot and later instructions name that slot in operand0/1.
     * slotOwners records "slot N was produced by node X"; AddConnection() uses
     * that fact to recreate each source -> target connector edge.
     */
    private static ReconstructedGraph ReconstructGraph(DecodedArtifact decoded)
    {
        var nodes = new List<FlowNode>();
        var connections = new List<PendingConnection>();
        var slotOwners = new Dictionary<ushort, FlowEndpoint>();
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new Dictionary<int, int>();

        for (var instructionIndex = 0; instructionIndex < decoded.Instructions.Count; instructionIndex++)
        {
            var instruction = decoded.Instructions[instructionIndex];
            var symbol = decoded.Symbols[instructionIndex];

            // Commit is an execution-boundary instruction, not a designer node.
            // It is therefore consumed/validated but never added to nodes[].
            if (instruction.Opcode == FlowOpcode.Commit)
            {
                ValidateFinalCommit(decoded.Instructions, symbol, instructionIndex);
                continue;
            }

            // MemoryCommit is an additional VM instruction for an existing source
            // Memory node. It contributes the Memory input connection but does not
            // create a second designer node.
            if (instruction.Opcode == FlowOpcode.MemoryCommit)
            {
                RequireSymbol(symbol, instructionIndex, 1);
                AddConnection(
                    connections,
                    slotOwners,
                    instruction.Operand0,
                    symbol.NodeId,
                    "in",
                    instructionIndex);
                continue;
            }

            RequireSymbol(symbol, instructionIndex, 0);

            var configuration = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var kind = ConfigureNode(
                decoded,
                instruction,
                configuration,
                instructionIndex);

            var inputs = AddInstructionConnections(
                connections,
                slotOwners,
                instruction,
                symbol.NodeId,
                instructionIndex);

            var depth = inputs.Count == 0
                ? 0
                : inputs.Max(slot => depths[slotOwners[slot].NodeId]) + 1;
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
            RegisterResultSlot(slotOwners, instruction, symbol.NodeId, instructionIndex);
        }

        return new ReconstructedGraph(nodes, connections);
    }

    private static void ValidateFinalCommit(
        List<Instruction> instructions,
        SymbolRecord symbol,
        int instructionIndex)
    {
        if (instructionIndex != instructions.Count - 1 || symbol.NodeId.Length != 0)
        {
            Fail(
                "invalid_instruction",
                $"/instructions/{instructionIndex}",
                "Commit must be the final anonymous instruction.");
        }
    }

    /*
     * Convert one VM opcode and its referenced tables back into the closest/current
     * designer node kind and configuration. Configure* helpers recover configuration
     * values stored indirectly in constants, point bindings, or state-slot records.
     */
    private static FlowNodeKind ConfigureNode(
        DecodedArtifact decoded,
        Instruction instruction,
        Dictionary<string, JsonElement> configuration,
        int instructionIndex)
    {
        return instruction.Opcode switch
        {
            FlowOpcode.PointInput => ConfigurePoint(
                configuration,
                decoded.Points,
                instruction.Auxiliary,
                DataDirection.Input,
                instructionIndex),
            FlowOpcode.DigitalConstant => ConfigureBoolean(
                FlowNodeKind.DigitalConstant,
                configuration,
                decoded.Constants,
                instruction.Auxiliary,
                instructionIndex),
            FlowOpcode.Not => FlowNodeKind.Not,
            FlowOpcode.And => FlowNodeKind.And,
            FlowOpcode.Or => FlowNodeKind.Or,
            FlowOpcode.Memory => ConfigureState(
                configuration,
                decoded.Slots,
                decoded.Constants,
                instruction.Auxiliary,
                instructionIndex),
            FlowOpcode.PointOutput => ConfigurePoint(
                configuration,
                decoded.Points,
                instruction.Auxiliary,
                DataDirection.Output,
                instructionIndex),
            FlowOpcode.Nand => FlowNodeKind.Nand,
            FlowOpcode.Nor => FlowNodeKind.Nor,
            FlowOpcode.Xor => FlowNodeKind.Xor,
            FlowOpcode.Xnor => FlowNodeKind.Xnor,
            FlowOpcode.NumericConstant => ConfigureNumber(
                FlowNodeKind.NumericConstant,
                configuration,
                decoded.Constants,
                instruction.Auxiliary,
                instructionIndex),
            FlowOpcode.Add => FlowNodeKind.Add,
            FlowOpcode.Comparator => ConfigureComparator(
                configuration,
                instruction.Auxiliary,
                instructionIndex),
            FlowOpcode.LevelShifter => ConfigureLevelShifter(
                configuration,
                decoded.Constants,
                instruction.Operand1,
                instruction.Auxiliary,
                instructionIndex),
            FlowOpcode.QualityGood => FlowNodeKind.QualityGood,
            FlowOpcode.OnDelay => ConfigureTimer(
                configuration,
                decoded.Slots,
                decoded.Constants,
                instruction.Auxiliary,
                instructionIndex),
            FlowOpcode.RisingEdge => FlowNodeKind.RisingEdge,
            _ => throw Error(
                "unsupported_opcode",
                $"/instructions/{instructionIndex}/opcode",
                $"Opcode {instruction.Opcode} cannot be represented by the designer.")
        };
    }

    /*
     * Recreate incoming graph edges from slot operands. Every operand slot must
     * already have an owner because transient reads are scheduled after their
     * producers. The returned operand list is also used to infer graph depth.
     */
    private static List<ushort> AddInstructionConnections(
        List<PendingConnection> connections,
        Dictionary<ushort, FlowEndpoint> slotOwners,
        Instruction instruction,
        string targetNodeId,
        int instructionIndex)
    {
        var inputs = new List<ushort>();

        switch (instruction.Opcode)
        {
            case FlowOpcode.Not:
                AddInputConnection("in", instruction.Operand0);
                break;

            case FlowOpcode.And:
            case FlowOpcode.Or:
            case FlowOpcode.Nand:
            case FlowOpcode.Nor:
            case FlowOpcode.Xor:
            case FlowOpcode.Xnor:
            case FlowOpcode.Add:
            case FlowOpcode.Comparator:
                AddInputConnection("a", instruction.Operand0);
                AddInputConnection("b", instruction.Operand1);
                break;

            case FlowOpcode.LevelShifter:
            case FlowOpcode.QualityGood:
            case FlowOpcode.OnDelay:
            case FlowOpcode.RisingEdge:
            case FlowOpcode.PointOutput:
                AddInputConnection("in", instruction.Operand0);
                break;
        }

        return inputs;

        void AddInputConnection(string portId, ushort slot)
        {
            AddConnection(
                connections,
                slotOwners,
                slot,
                targetNodeId,
                portId,
                instructionIndex);

            inputs.Add(slot);
        }
    }

    /*
     * Register this instruction as the unique producer of its result slot.
     * Once recorded, any later operand that names this u16 can be translated
     * back into a designer connection from this node.
     */
    private static void RegisterResultSlot(
        Dictionary<ushort, FlowEndpoint> slotOwners,
        Instruction instruction,
        string nodeId,
        int instructionIndex)
    {
        if (instruction.ResultSlotIndex == Unused || slotOwners.ContainsKey(instruction.ResultSlotIndex))
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instructionIndex}/result",
                "A node result must write one unique slot.");
        }

        slotOwners[instruction.ResultSlotIndex] = new FlowEndpoint(nodeId, "value");
    }

    private static Flow BuildRecoveredFlow(
        DecodedArtifact decoded,
        ReconstructedGraph graph,
        string? name)
    {
        return new Flow
        {
            Id = decoded.FlowId,
            Name = string.IsNullOrWhiteSpace(name) ? Label(decoded.FlowId) : name.Trim(),
            Description = $"Recovered from Flow IL v1 revision {U32(decoded.Bytes, 16)}.",
            UpdatedAt = "1970-01-01T00:00:00Z",
            Nodes = graph.Nodes,
            Connections = [.. graph.Connections.Select((item, index) =>
                new FlowConnection(
                    $"connection-{index + 1:D3}",
                    item.Source,
                    new FlowEndpoint(item.TargetNodeId, item.TargetPortId)))]
        };
    }

    /*
     * Validate and decode the artifact framing.
     *
     * Byte map used by this implementation:
     *   0..3    magic "FIL1"
     *   4..5    IL version u16 LE
     *   6..7    envelope size u16 LE (must be 128)
     *   8..11   exact artifact size u32 LE
     *   26..27  section count u16 LE (must be 8)
     *   116..119 directory offset u32 LE (must be 128)
     *
     * The directory begins at byte 128 and contains eight 48-byte entries.
     * Each entry's offset/length is checked BEFORE slicing, and SHA-256 is verified
     * over the exact payload bytes. Contiguity is enforced by expectedOffset.
     */
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

        // 128 + (8 * 48) = 512: canonical start of section 1 when there are 8 entries.
        var expectedOffset = EnvelopeBytes + (SectionCount * DirectoryEntryBytes);

        for (var index = 0; index < SectionCount; index++)
        {
            // Directory entry N lives at 128 + N*48 and is always exactly 48 bytes.
            var entry = bytes.Slice(EnvelopeBytes + (index * DirectoryEntryBytes), DirectoryEntryBytes);

            // Entry layout:
            //   0..1   id u16 LE
            //   2..3   section version u16 LE
            //   4..7   absolute payload offset u32 LE
            //   8..11  payload byte length u32 LE
            //   12..15 logical record count u32 LE
            //   16..47 SHA-256 digest (raw 32 bytes)
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

            // Hash only the payload range declared for this section and compare
            // with the 32 raw digest bytes embedded in this directory entry.
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

    /*
     * SECTION 1 decoder.
     * First consume a four-byte prefix [type, flags/value, reserved:u16].
     * Boolean ends there (4 bytes). Number consumes an additional 8-byte f64.
     */
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

    /*
     * SECTION 2 decoder.
     * Physical record begins with four fixed bytes, then two variable string8 fields.
     * SectionReader advances its private byte cursor as each piece is consumed.
     */
    private static List<PointRecord> ReadPoints(SectionReader reader)
    {
        var values = new List<PointRecord>();

        for (var i = 0; i < reader.Count; i++)
        {
            var prefix = reader.Fixed(4, $"/points/{i}");
            var id = reader.String8($"/points/{i}/id");
            var units = reader.String8AllowEmpty($"/points/{i}/units");

            var direction = (DataDirection)prefix[0];
            var dataType = (DataType)prefix[1];
            var qualityPolicy = (InputQualityPolicy)prefix[2];
            var bindingKind = (PointBindingKind)prefix[3];

            if (direction is not (DataDirection.Input or DataDirection.Output)
                || dataType is not (DataType.Boolean or DataType.Number)
                || qualityPolicy is not (
                    InputQualityPolicy.RequireGood or
                    InputQualityPolicy.Propagate)
                || bindingKind is not (
                    PointBindingKind.ControllerPoint or
                    PointBindingKind.FlowInterface))
            {
                Fail(
                    "unsupported_point",
                    $"/points/{i}",
                    "Point binding type is unsupported.");
            }

            values.Add(new PointRecord(
                direction,
                dataType,
                qualityPolicy,
                bindingKind,
                id,
                units));
        }

        reader.End("/points");
        return values;
    }

    /*
     * SECTION 3 decoder — fixed 8-byte records:
     *   0 kind, 1 type, 2..3 flags, 4..5 slot index, 6..7 initial constant.
     */
    private static Dictionary<ushort, SlotRecord> ReadSlots(SectionReader reader)
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

    /*
     * SECTION 4 decoder — fixed 12-byte instruction records:
     *
     *   0      opcode:u8
     *   1      flags:u8 (must be zero)
     *   2..3   result:u16 LE
     *   4..5   operand0:u16 LE
     *   6..7   operand1:u16 LE
     *   8..9   auxiliary:u16 LE
     *   10..11 reserved:u16 LE (must be zero)
     *
     * This is the direct inverse of FlowCompiler.EncodeV1Instruction().
     */
    private static List<Instruction> ReadInstructions(SectionReader reader)
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

    /*
     * SECTION 6 decoder.
     * Variable-length symbol records carry the source identity that section 4 does
     * not contain: node ID, lowering discriminator, label, canvas coordinates and
     * group ID. There must be exactly one symbol record per instruction.
     */
    private static List<SymbolRecord> ReadSymbols(SectionReader reader, int instructionCount)
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

    private static List<Dependency> ReadDependencies(SectionReader reader)
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
            _ = reader.Fixed(4, $"/debugMap/{i}");
            _ = reader.String8($"/debugMap/{i}/nodeId");
        }

        reader.End("/debugMap");
    }

    /*
     * Recover a point/input-output node from the binding-table index stored in the
     * instruction auxiliary field. The binding direction is checked because the same
     * PointInput/PointOutput encoding is used for both Boolean and numeric points.
     */
    private static FlowNodeKind ConfigurePoint(
        Dictionary<string, JsonElement> configuration,
        IReadOnlyList<PointRecord> points,
        ushort pointIndex,
        DataDirection direction,
        int instructionIndex)
    {
        if (pointIndex >= points.Count || points[pointIndex].Direction != direction)
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instructionIndex}/auxiliary",
                "Point binding is missing or has the wrong direction.");
        }

        var point = points[pointIndex];

        return point.BindingKind switch
        {
            PointBindingKind.FlowInterface =>
                ConfigureInterface(),

            PointBindingKind.ControllerPoint =>
                ConfigureControllerPoint(),

            _ => throw Error(
                "unsupported_point",
                $"/instructions/{instructionIndex}/auxiliary",
                "Point binding kind is unsupported.")
        };

        FlowNodeKind ConfigureInterface()
        {
            configuration["interfaceId"] =
                JsonSerializer.SerializeToElement(point.Id);

            if (!string.IsNullOrEmpty(point.Units))
            {
                configuration["units"] =
                    JsonSerializer.SerializeToElement(point.Units);
            }

            return direction == DataDirection.Input
                ? FlowNodeKind.FlowInput
                : FlowNodeKind.FlowOutput;
        }

        FlowNodeKind ConfigureControllerPoint()
        {
            configuration["pointId"] =
                JsonSerializer.SerializeToElement(point.Id);

            if (!string.IsNullOrEmpty(point.Units))
            {
                configuration["units"] =
                    JsonSerializer.SerializeToElement(point.Units);
            }

            return point.DataType == DataType.Number
                ? direction == DataDirection.Input
                    ? FlowNodeKind.AnalogInput
                    : FlowNodeKind.AnalogOutput
                : direction == DataDirection.Input
                    ? FlowNodeKind.DigitalInput
                    : FlowNodeKind.DigitalOutput;
        }
    }

    /*
     * Recover a Boolean constant node configuration from the constant-pool index
     * stored in the instruction auxiliary field.
     */
    private static FlowNodeKind ConfigureBoolean(
        FlowNodeKind kind,
        Dictionary<string, JsonElement> configuration,
        IReadOnlyList<ConstantRecord> constants,
        ushort constantIndex,
        int instructionIndex)
    {
        if (constantIndex >= constants.Count || constants[constantIndex].DataType != DataType.Boolean)
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instructionIndex}/auxiliary",
                "Boolean constant index is out of range.");
        }

        configuration["value"] = JsonSerializer.SerializeToElement(constants[constantIndex].Number != 0D);

        return kind;
    }

    /*
     * Recover a Memory node's initial value from the state-slot record referenced by
     * the Memory instruction. The state slot then points to the numeric constant that
     * was used to initialise that state when the artifact was compiled.
     */
    private static FlowNodeKind ConfigureState(
        Dictionary<string, JsonElement> configuration,
        IReadOnlyDictionary<ushort, SlotRecord> slots,
        IReadOnlyList<ConstantRecord> constants,
        ushort stateSlotIndex,
        int instructionIndex)
    {
        if (!slots.TryGetValue(stateSlotIndex, out var slot)
            || slot.Kind != FlowSlotKind.MemoryState
            || slot.InitialConstant >= constants.Count
            || constants[slot.InitialConstant].DataType != DataType.Number)
        {
            throw Error(
                "invalid_operand",
                $"/instructions/{instructionIndex}/auxiliary",
                "State slot is invalid.");
        }

        configuration["value"] = JsonSerializer.SerializeToElement(constants[slot.InitialConstant].Number);

        return FlowNodeKind.Memory;
    }

    /*
     * Recover a numeric constant node configuration from its constant-pool index.
     */
    private static FlowNodeKind ConfigureNumber(
        FlowNodeKind kind,
        Dictionary<string, JsonElement> configuration,
        IReadOnlyList<ConstantRecord> constants,
        ushort constantIndex,
        int instructionIndex)
    {
        if (constantIndex >= constants.Count || constants[constantIndex].DataType != DataType.Number)
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instructionIndex}/auxiliary",
                "Numeric constant index is out of range.");
        }

        configuration["value"] = JsonSerializer.SerializeToElement(constants[constantIndex].Number);

        return kind;
    }

    /*
     * Convert the compact comparator code stored in the instruction back to the
     * designer's textual operator value.
     */
    private static FlowNodeKind ConfigureComparator(
        Dictionary<string, JsonElement> configuration,
        ushort comparatorCode,
        int instructionIndex)
    {
        var comparator = comparatorCode switch
        {
            1 => "lt",
            2 => "lte",
            3 => "eq",
            4 => "gte",
            5 => "gt",
            6 => "ne",
            _ => null
        };

        if (comparator is null)
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instructionIndex}/auxiliary",
                "Comparison operator is invalid.");
        }

        configuration["operator"] = JsonSerializer.SerializeToElement(comparator);

        return FlowNodeKind.Comparator;
    }

    /*
     * LevelShifter stores gain and offset as references into the numeric constant
     * pool. Both references must resolve to numeric constants before reconstructing
     * the designer configuration.
     */
    private static FlowNodeKind ConfigureLevelShifter(
        Dictionary<string, JsonElement> configuration,
        IReadOnlyList<ConstantRecord> constants,
        ushort gainConstantIndex,
        ushort offsetConstantIndex,
        int instructionIndex)
    {
        if (gainConstantIndex >= constants.Count
            || offsetConstantIndex >= constants.Count
            || constants[gainConstantIndex].DataType != DataType.Number
            || constants[offsetConstantIndex].DataType != DataType.Number)
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instructionIndex}",
                "Level-shifter constants are invalid.");
        }

        configuration["gain"] = JsonSerializer.SerializeToElement(constants[gainConstantIndex].Number);
        configuration["offset"] = JsonSerializer.SerializeToElement(constants[offsetConstantIndex].Number);

        return FlowNodeKind.LevelShifter;
    }

    /*
     * Recover timer configuration from the TimerState slot referenced by the VM
     * instruction. The slot's initial constant contains the configured duration.
     */
    private static FlowNodeKind ConfigureTimer(
        Dictionary<string, JsonElement> configuration,
        IReadOnlyDictionary<ushort, SlotRecord> slots,
        IReadOnlyList<ConstantRecord> constants,
        ushort stateSlotIndex,
        int instructionIndex)
    {
        if (!slots.TryGetValue(stateSlotIndex, out var slot)
            || slot.Kind != FlowSlotKind.TimerState
            || slot.InitialConstant >= constants.Count
            || constants[slot.InitialConstant].DataType != DataType.Number)
        {
            Fail(
                "invalid_operand",
                $"/instructions/{instructionIndex}/timer",
                "Timer state is invalid.");
        }

        // Fail(...) always throws, so reaching this point means TryGetValue succeeded.
        // The null-forgiving operator communicates that control-flow fact to nullable
        // analysis without changing the runtime value.
        var timerState = slot!;

        configuration["durationMs"] = JsonSerializer.SerializeToElement(constants[timerState.InitialConstant].Number);

        return FlowNodeKind.OnDelay;
    }

    /*
     * Convert a binary operand slot number into a designer graph edge.
     *
     * Each ordinary instruction registers its result slot in 'slotOwners'. A later
     * instruction therefore identifies its upstream producer using only the u16
     * operand slot encoded in section 4. A missing owner means the artifact contains
     * a forward/invalid transient read and cannot represent a valid scheduled graph.
     */
    private static void AddConnection(
        ICollection<PendingConnection> connections,
        Dictionary<ushort, FlowEndpoint> slotOwners,
        ushort sourceSlotIndex,
        string targetNodeId,
        string targetPortId,
        int instructionIndex)
    {
        if (!slotOwners.TryGetValue(sourceSlotIndex, out var source))
        {
            throw Error(
                "invalid_operand",
                $"/instructions/{instructionIndex}",
                "An input does not reference an earlier node result.");
        }

        connections.Add(new PendingConnection(source, targetNodeId, targetPortId));
    }

    /*
     * Recreate the connector definitions expected by the designer for a recovered
     * node kind. These are authoring-model ports, not the VM slot references used
     * while decoding section 4.
     */
    private static IReadOnlyList<FlowConnector> Connectors(FlowNodeKind kind)
    {
        return kind switch
        {
            FlowNodeKind.DigitalInput or FlowNodeKind.DigitalConstant => [BooleanOutput("value", "Value")],
            FlowNodeKind.AnalogInput => [NumberOutput("value", "Value")],
            FlowNodeKind.DigitalOutput => [BooleanInput("in", "Input")],
            FlowNodeKind.AnalogOutput => [NumberInput("in", "Input")],
            FlowNodeKind.Not => [BooleanInput("in", "Input"), BooleanOutput("value", "Value")],
            FlowNodeKind.And or FlowNodeKind.Or or FlowNodeKind.Nand or FlowNodeKind.Nor or FlowNodeKind.Xor or FlowNodeKind.Xnor =>
                [BooleanInput("a", "A"), BooleanInput("b", "B"), BooleanOutput("value", "Value")],
            FlowNodeKind.Memory => [NumberInput("in", "Input"), NumberOutput("value", "Previous value")],
            FlowNodeKind.NumericConstant => [NumberOutput("value", "Value")],
            FlowNodeKind.Add => [NumberInput("a", "A"), NumberInput("b", "B"), NumberOutput("value", "Value")],
            FlowNodeKind.Comparator => [NumberInput("a", "A"), NumberInput("b", "B"), BooleanOutput("value", "Value")],
            FlowNodeKind.LevelShifter => [NumberInput("in", "Input"), NumberOutput("value", "Value")],
            FlowNodeKind.QualityGood or FlowNodeKind.OnDelay or FlowNodeKind.RisingEdge =>
                [BooleanInput("in", "Input"), BooleanOutput("value", "Value")],
            _ => []
        };
    }

    private static FlowConnector BooleanInput(string id, string label)
    {
        return new(id, label, DataDirection.Input, DataType.Boolean, "left");
    }

    private static FlowConnector BooleanOutput(string id, string label)
    {
        return new(id, label, DataDirection.Output, DataType.Boolean, "right");
    }

    private static FlowConnector NumberInput(string id, string label)
    {
        return new(id, label, DataDirection.Input, DataType.Number, "left");
    }

    private static FlowConnector NumberOutput(string id, string label)
    {
        return new(id, label, DataDirection.Output, DataType.Number, "right");
    }

    private static string Label(string value)
    {
        return string.Join(
            ' ',
            value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    /*
     * Verify that an instruction which represents a designer node has symbol metadata
     * for that node and the expected instruction discriminator.
     */
    private static void RequireSymbol(SymbolRecord symbol, int index, byte discriminator)
    {
        if (symbol.NodeId.Length == 0 || symbol.Discriminator != discriminator)
        {
            Fail("invalid_symbols", $"/symbols/{index}", "Instruction symbol cannot be represented as a designer node.");
        }
    }

    /*
     * Read the flow identifier from the fixed envelope field at bytes 52..115. The
     * first byte is the UTF-8 byte length and the remaining bytes contain the ID.
     */
    private static string ReadFlowId(ReadOnlySpan<byte> bytes)
    {
        var length = bytes[52];

        if (length is 0 or > 63)
        {
            Fail("invalid_identifier", "/flowId", "Flow ID length is invalid.");
        }

        return Encoding.UTF8.GetString(bytes.Slice(53, length));
    }

    /*
     * Create a bounded reader over exactly one directory-declared section payload.
     * Copying the payload gives SectionReader an isolated byte range whose End()
     * check can reliably reject trailing bytes.
     */
    private static SectionReader Section(ReadOnlySpan<byte> artifact, SectionInfo[] sections, int id)
    {
        var section = sections[id - 1];

        return new SectionReader(
            artifact.Slice(section.Offset, section.Length).ToArray(),
            section.Count,
            section.Version);
    }

    // Primitive little-endian field readers. The caller supplies a BYTE offset.
    private static ushort U16(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    }

    private static FlowDecompilationException Error(string code, string path, string message)
    {
        return new(new(code, path, message));
    }

    private static void Fail(string code, string path, string message)
    {
        throw Error(code, path, message);
    }

    /*
     * Small bounded cursor for one section payload.
     *
     * The directory says how many logical records should exist (Count), while this
     * reader tracks how many BYTES have actually been consumed (_offset). End()
     * requires the cursor to land exactly on bytes.Length, so trailing garbage is
     * rejected rather than silently ignored.
     */
    private sealed class SectionReader(byte[] bytes, int count, ushort version)
    {
        private int _offset;

        public int Count { get; } = count;

        public ushort Version { get; } = version;

        // Consume exactly 'length' bytes at the current byte cursor. The subtraction
        // form of the check avoids relying on potentially overflowing offset+length.
        public ReadOnlySpan<byte> Fixed(int length, string path)
        {
            if (length < 0 || _offset > bytes.Length - length)
            {
                Fail("malformed_section", path, "Section record is truncated.");
            }

            var result = bytes.AsSpan(_offset, length);
            _offset += length;

            return result;
        }

        public string String8(string path)
        {
            var value = String8AllowEmpty(path);

            if (value.Length == 0)
            {
                Fail("invalid_identifier", path, "Identifier must not be empty.");
            }

            return value;
        }

        // string8 has no terminator: [UTF-8 byte count:u8][exact UTF-8 bytes].
        // The length is a byte count, not a C# character count.
        public string String8AllowEmpty(string path)
        {
            var length = Fixed(1, path)[0];
            var value = Encoding.UTF8.GetString(Fixed(length, path));

            if (Encoding.UTF8.GetByteCount(value) != length)
            {
                Fail("invalid_identifier", path, "Identifier is not canonical UTF-8.");
            }

            return value;
        }

        // Record-count parsing is accepted only if the byte cursor also lands on
        // the exact directory-declared section boundary. Trailing bytes are invalid.
        public void End(string path)
        {
            if (_offset != bytes.Length)
            {
                Fail("malformed_section", path, "Section has trailing bytes.");
            }
        }

        public double F64(string path)
        {
            var bits = BinaryPrimitives.ReadInt64LittleEndian(Fixed(8, path));
            var value = BitConverter.Int64BitsToDouble(bits);

            if (!double.IsFinite(value))
            {
                Fail("invalid_authoring_metadata", path, "Authoring coordinates must be finite.");
            }

            return value;
        }
    }

    /*
     * Fully decoded binary tables needed to reconstruct a designer Flow. Keeping this
     * as one immutable value prevents the graph reconstruction helpers from carrying
     * a long parameter list and makes the boundary between parsing and reconstruction
     * explicit.
     */
    private sealed record DecodedArtifact(
        byte[] Bytes,
        string FlowId,
        List<ConstantRecord> Constants,
        List<PointRecord> Points,
        Dictionary<ushort, SlotRecord> Slots,
        List<Instruction> Instructions,
        List<SymbolRecord> Symbols,
        List<Dependency> Dependencies,
        Dependency Template);

    /*
     * Intermediate designer graph reconstructed from instruction slot references.
     * Flow-level metadata is applied afterwards by BuildRecoveredFlow().
     */
    private sealed record ReconstructedGraph(
        List<FlowNode> Nodes,
        List<PendingConnection> Connections);

    /* Directory metadata needed to locate and parse one validated section payload. */
    private sealed record SectionInfo(int Offset, int Length, int Count, ushort Version);

    /* Decoded point/interface binding from section 2. */
    private sealed record PointRecord(
        DataDirection Direction,
        DataType DataType,
        InputQualityPolicy QualityPolicy,
        PointBindingKind BindingKind,
        string Id,
        string Units);

    /* Decoded typed constant from section 1. */
    private sealed record ConstantRecord(DataType DataType, double Number);

    /* Decoded state/transient slot layout record from section 3. */
    private sealed record SlotRecord(FlowSlotKind Kind, byte Type, ushort InitialConstant);

    /* Logical decoded form of one fixed 12-byte VM instruction record. */
    internal sealed record Instruction(
        FlowOpcode Opcode,
        ushort ResultSlotIndex,
        ushort Operand0,
        ushort Operand1,
        ushort Auxiliary);

    /* Authoring identity/layout metadata associated with one instruction. */
    private sealed record SymbolRecord(byte Discriminator, string NodeId, string Label, double X, double Y, double ZOrder, string GroupId);

    /* Source dependency record used to recover controller-template provenance. */
    private sealed record Dependency(byte Kind, string Id, uint Revision);

    /* Graph edge held until stable recovered connection IDs are assigned. */
    private sealed record PendingConnection(FlowEndpoint Source, string TargetNodeId, string TargetPortId);
}