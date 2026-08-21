/*
 * FlowCompiler
 * ========================================
 *
 * The comments concentrate on the Flow IL binary-production pipeline:
 *
 *   designer/source graph
 *       -> validation and deterministic scheduling
 *       -> canonical tables (constants, points, slots)
 *       -> scheduled 12-byte instruction records
 *       -> commit/symbol/debug/dependency records
 *       -> eight section byte streams
 *       -> eight 48-byte section-directory entries
 *       -> fixed 128-byte "FIL1" envelope
 *       -> final contiguous artifact
 *
 * Integer serialization in this implementation is little-endian. Variable-length
 * identifiers use the implementation's string8 form: one byte of UTF-8 byte count
 * followed immediately by that many UTF-8 bytes. The final artifact has no implicit
 * object layout, CLR metadata, alignment padding, or pointers: every byte is emitted
 * explicitly by helpers such as U16(), U32(), F64(), String8(), Concat(), and the
 * section encoders below.
 *
 * IMPORTANT: These comments describe what THIS CODE does. They do not alter code to
 * reconcile any discrepancy with a prose contract or another implementation.
 */

/*
 * SLOT MODEL
 * ==========
 *
 * Slots are the VM's numbered storage locations. Instructions do not refer to
 * source nodes directly when reading and writing values; they refer to slot
 * indices.
 *
 * A useful mental model is:
 *
 *     slot = a numbered variable/register in the Flow VM
 *
 *
 * TRANSIENT SLOTS
 * ---------------
 *
 * Every scheduled node is assigned a transient result slot. This slot holds
 * the value produced by that node while the current scan is executing.
 *
 * For example, given:
 *
 *     InputA ----\
 *                 Add ----> Output
 *     InputB ----/
 *
 * the compiler may allocate:
 *
 *     +-------+---------+-----------------------+
 *     | Slot  | Node    | Purpose               |
 *     +-------+---------+-----------------------+
 *     |   0   | InputA  | InputA result         |
 *     |   1   | InputB  | InputB result         |
 *     |   2   | Add     | Add result            |
 *     |   3   | Output  | Output node result    |
 *     +-------+---------+-----------------------+
 *
 * During execution, the slots can be pictured as temporary working memory:
 *
 *                         VM working slots
 *
 *                         0     1     2     3
 *                       +-----+-----+-----+-----+
 *     after InputA      | 10  |  ?  |  ?  |  ?  |
 *                       +-----+-----+-----+-----+
 *
 *     after InputB      | 10  | 20  |  ?  |  ?  |
 *                       +-----+-----+-----+-----+
 *
 *     after Add         | 10  | 20  | 30  |  ?  |
 *                       +-----+-----+-----+-----+
 *
 *     after Output      | 10  | 20  | 30  | 30  |
 *                       +-----+-----+-----+-----+
 *
 * The Add instruction therefore does not need to know that its inputs came
 * from nodes named "InputA" and "InputB". The compiler has already translated
 * those graph connections into slot numbers.
 *
 * Conceptually the generated instruction becomes:
 *
 *     ADD
 *         operand0 = slot 0
 *         operand1 = slot 1
 *         result   = slot 2
 *
 * or, expressed like an assignment:
 *
 *     slot[2] = slot[0] + slot[1]
 *
 *
 * WHY SCHEDULE ORDER MATTERS
 * --------------------------
 *
 * Transient slots are allocated in deterministic schedule order:
 *
 *     schedule[0]  ---> slot 0
 *     schedule[1]  ---> slot 1
 *     schedule[2]  ---> slot 2
 *     ...
 *
 * Because the schedule guarantees that dependencies execute before the nodes
 * which consume them, an instruction can read the result slots produced by
 * earlier instructions.
 *
 *     InputA        InputB
 *        |             |
 *        v             v
 *     slot[0]       slot[1]
 *        \             /
 *         \           /
 *          v         v
 *             ADD
 *              |
 *              v
 *           slot[2]
 *              |
 *              v
 *            Output
 *
 *
 * TRANSIENT SLOTS VS STATE SLOTS
 * ------------------------------
 *
 * Not all slots have the same lifetime.
 *
 *     +----------------+-----------------------------------------------+
 *     | Slot type      | Purpose                                       |
 *     +----------------+-----------------------------------------------+
 *     | Transient      | Temporary working value for the current scan  |
 *     | State          | State that must be retained between scans     |
 *     +----------------+-----------------------------------------------+
 *
 * A normal arithmetic or logic node only needs its transient result:
 *
 *     scan N
 *
 *         Input ---> slot[0]
 *                      |
 *                      v
 *                    Logic ---> slot[1]
 *
 *     scan ends
 *
 *         transient working values are no longer needed as previous-scan
 *         working values; the next scan computes its own results.
 *
 * Stateful operations additionally require storage whose value has meaning
 * across scan boundaries. Timers, edge detection, and other stateful
 * operations use state slots for this purpose.
 *
 * State slots are allocated after the transient result slots:
 *
 *     +-------------------------------------+
 *     | transient result slots              |
 *     |                                     |
 *     | slot 0   scheduled node 0 result    |
 *     | slot 1   scheduled node 1 result    |
 *     | slot 2   scheduled node 2 result    |
 *     | ...                                 |
 *     +-------------------------------------+
 *     | state slots                         |
 *     |                                     |
 *     | slot N     stateful node state      |
 *     | slot N+1   stateful node state      |
 *     | ...                                 |
 *     +-------------------------------------+
 *
 * Therefore, a node can have both:
 *
 *     +---------------------------+
 *     | Stateful node             |
 *     +---------------------------+
 *          |                |
 *          |                |
 *          v                v
 *     transient slot     state slot
 *          |                |
 *          |                +-- retained state used across scans
 *          |
 *          +-- result produced during the current scan
 *
 *
 * SLOT INDICES IN THE BINARY
 * --------------------------
 *
 * The artifact stores slot references as numeric indices rather than source
 * node IDs. Flow instructions contain fields such as:
 *
 *     +-----------+----------+----------+----------+-----------+
 *     | result    | operand0 | operand1 | auxiliary| ...       |
 *     +-----------+----------+----------+----------+-----------+
 *        slot        slot       slot       slot/
 *        index       index      index      other index
 *
 * Each of these index fields is encoded as a 16-bit unsigned integer.
 * 0xFFFF is used where an instruction does not use a particular index.
 *
 * In short:
 *
 *     Designer graph
 *          |
 *          | connections between node ports
 *          v
 *     deterministic schedule
 *          |
 *          | assigns result locations
 *          v
 *     numbered VM slots
 *          |
 *          | referenced by instruction operands/results
 *          v
 *     VM execution
 *
 * This removes the need for the runtime VM to resolve designer node names or
 * graph connections while executing the flow. The compiler performs that work
 * ahead of time and the VM executes using compact numeric slot references.
 */

using Server.Common.Contracts;
using Server.Compiler.Contracts;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Compiler.Services.Implementation;

internal sealed partial class FlowCompiler : IFlowCompiler
{
    /*
     * NODE PORT SHAPES
     * ================
     *
     * Defines the expected input/output port layout for every supported node kind.
     *
     * Each FlowNodeKind maps to a FlowNodeShape definition describing:
     *
     *     - which ports the node has
     *     - each port's ID/name
     *     - whether the port is an input or output
     *     - the data type carried by the port
     *
     * For example:
     *
     *     FlowNodeKind.And
     *
     *          a ----\
     *                 AND ----> value
     *          b ----/
     *
     *     is described as:
     *
     *         +-------+-----------+---------+
     *         | Port  | Direction | Type    |
     *         +-------+-----------+---------+
     *         | a     | Input     | Boolean |
     *         | b     | Input     | Boolean |
     *         | value | Output    | Boolean |
     *         +-------+-----------+---------+
     *
     * The compiler uses these definitions when validating the source graph. A
     * connection can therefore be checked to ensure that:
     *
     *     - the referenced port actually exists on the node
     *     - connections run from an output port to an input port
     *     - the source and target ports have compatible data types
     *
     * In other words, Shapes describes the compiler's expected "connection
     * interface" for each kind of node. It defines how each node is allowed to
     * connect to the rest of the flow graph; it does not contain the node's
     * runtime value or execution state.
     */
    private static readonly Dictionary<FlowNodeKind, FlowNodeShape> Shapes = new()
    {
        [FlowNodeKind.DigitalInput] = new([new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.AnalogInput] = new([new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.DigitalConstant] = new([new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Not] = new([new("in", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.And] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Or] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Nand] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Nor] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Xor] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Xnor] = new([new("a", DataDirection.Input, DataType.Boolean), new("b", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.NumericConstant] = new([new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Add] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Comparator] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.LevelShifter] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.QualityGood] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.OnDelay] = new([new("in", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.RisingEdge] = new([new("in", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Memory] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.FlowInput] = new([new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.FlowOutput] = new([new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.DigitalOutput] = new([new("in", DataDirection.Input, DataType.Boolean)]),
        [FlowNodeKind.AnalogOutput] = new([new("in", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Average] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Calculator] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Clamp] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Min] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Max] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Line] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.If] = new([new("condition", DataDirection.Input, DataType.Boolean), new("whenTrue", DataDirection.Input, DataType.Boolean), new("whenFalse", DataDirection.Input, DataType.Boolean), new("value", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Selector] = new([new("condition", DataDirection.Input, DataType.Boolean), new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Split] = new([new("input", DataDirection.Input, DataType.Number), new("output", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Sequence] = new([new("a", DataDirection.Input, DataType.Number), new("b", DataDirection.Input, DataType.Number), new("value", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Override] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Delay] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Timer] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Pulse] = new([new("input", DataDirection.Input, DataType.Boolean), new("output", DataDirection.Output, DataType.Boolean)]),
        [FlowNodeKind.Schedule] = new([new("output", DataDirection.Output, DataType.Number)]),
        [FlowNodeKind.Calendar] = new([new("output", DataDirection.Output, DataType.Number)])
    };

    public FlowCompilationResult Compile(FlowCompilationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ArtifactVersion != FlowILV1Format.Version)
        {
            throw Failure(FlowCompilationDiagnosticCode.UnsupportedArtifactVersion, "/artifactVersion", FlowILV1Format.Version);
        }

        var obsoleteNodeIndex = request.Source.Nodes
            .Select((node, index) => (node, index))
            .FirstOrDefault(item => item.node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput);
        if (obsoleteNodeIndex.node is not null)
        {
            throw Failure(
                FlowCompilationDiagnosticCode.UnsupportedNode,
                $"/nodes/{obsoleteNodeIndex.index}/kind",
                obsoleteNodeIndex.node.Kind);
        }

        Validate(request);

        return CompileFlowIlV1(request);
    }

    public void WriteBinary(FlowCompilationResult compilation, string path)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllBytes(path, compilation.Artifact.Span);
    }

    public void WriteIntelHex(
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
                    (byte)IntelHexRecordType.ExtendedLinearAddress,
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
                (byte)IntelHexRecordType.Data,
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
            throw Failure(FlowCompilationDiagnosticCode.UnsupportedSourceSchema, "/schemaVersion", 1);
        }

        ValidateIdentifier(source.Id, "/id", 63);
        ValidateIdentifier(source.ControllerTemplateId, "/controllerTemplateId", 31);

        if (source.Revision == 0)
        {
            throw Failure(FlowCompilationDiagnosticCode.InvalidFlowRevision, "/revision");
        }

        if (source.ControllerTemplateRevision == 0)
        {
            throw Failure(FlowCompilationDiagnosticCode.InvalidControllerTemplateRevision, "/controllerTemplateRevision");
        }

        var target = request.Target.ControllerTemplate.Source;
        if (!string.Equals(source.ControllerTemplateId, target.Id, StringComparison.Ordinal))
        {
            throw Failure(
                FlowCompilationDiagnosticCode.ControllerTemplateIdMismatch,
                "/controllerTemplateId",
                target.Id,
                source.ControllerTemplateId
            );
        }

        if (target.Revision < 0 || (uint)target.Revision != source.ControllerTemplateRevision)
        {
            throw Failure(
                FlowCompilationDiagnosticCode.ControllerTemplateRevisionMismatch,
                "/controllerTemplateRevision",
                source.ControllerTemplateRevision,
                target.Revision
            );
        }

        if (source.Execution.Mode != FlowExecutionMode.Manual
            || source.Execution.IntervalMs != 0
            || source.Execution.InputQualityPolicy is not (InputQualityPolicy.RequireGood or InputQualityPolicy.Propagate))
        {
            throw Failure(FlowCompilationDiagnosticCode.UnsupportedExecution, "/execution", 1);
        }

        if (source.Nodes.Count is < 1 or > 128)
        {
            throw Failure(FlowCompilationDiagnosticCode.NodeCountOutOfRange, "/nodes", 1, 128);
        }

        if (source.Connections.Count > 384)
        {
            throw Failure(FlowCompilationDiagnosticCode.ConnectionCountLimitExceeded, "/connections", 384);
        }

        ValidateInterface(source);

        ValidateGraph(source);
        ValidateUnits(request);
    }

    /*
     * Build one complete Flow IL v1 artifact.
     *
     * Physical byte layout produced by this method:
     *
     *   +-------------------------------+ offset 0
     *   | 128-byte envelope             |
     *   +-------------------------------+ offset 128
     *   | directory[0], 48 bytes        | section 1 metadata + SHA-256
     *   | directory[1], 48 bytes        | section 2 metadata + SHA-256
     *   | ...                           |
     *   | directory[7], 48 bytes        | section 8 metadata + SHA-256
     *   +-------------------------------+ offset 128 + 8*48 = 512
     *   | section 1 bytes               |
     *   | section 2 bytes               |
     *   | ...                           |
     *   | section 8 bytes               |
     *   +-------------------------------+ exact artifact length
     *
     * There are no separators between sections. Their boundaries are carried only
     * by the directory's offset/length fields. Consequently, section construction
     * and directory offset calculation must agree exactly.
     */
    private static FlowCompilationResult CompileFlowIlV1(FlowCompilationRequest request)
    {
        /*
         * The compilation pipeline is intentionally split into distinct stages here.
         * Each helper owns one concern so this method describes the overall process
         * without also containing every byte-level encoding detail.
         *
         *     validated source
         *          |
         *          v
         *     compilation model
         *          |
         *          v
         *     eight encoded sections
         *          |
         *          v
         *     section directory + envelope
         *          |
         *          v
         *     final contiguous artifact
         */
        var model = PrepareCompilationModel(request);
        var sections = BuildSections(request, model);
        var directory = BuildSectionDirectory(sections, out var artifactLength);
        var capabilities = DetermineRequiredCapabilities(model.Source, model.Points, model.MemoryIds);
        var workingBytes = checked((uint)((model.Schedule.Count + model.StateIds.Length) * 32));
        var envelope = BuildEnvelope(
            model.Source,
            sections.Length,
            model.Instructions.Count,
            artifactLength,
            capabilities,
            workingBytes);

        // Final byte build. This is the only place the complete artifact is assembled:
        //   [128-byte envelope]
        //   [384-byte directory when section count is 8]
        //   [section 1 bytes][section 2 bytes]...[section 8 bytes]
        var artifact = Concat(
            envelope,
            Concat([.. directory]),
            Concat([.. sections.Select(section => section.Bytes)]));

        if (sections.Length != FlowILV1Format.SectionCount)
        {
            throw new InvalidOperationException(
                $"Flow IL v1 requires exactly {FlowILV1Format.SectionCount} sections.");
        }

        return new FlowCompilationResult
        {
            ArtifactVersion = 1,
            Artifact = artifact,
            ArtifactSha256 = Convert.ToHexStringLower(SHA256.HashData(artifact)),
            FlowRevision = model.Source.Revision,
            ControllerTemplateId = model.Source.ControllerTemplateId,
            ControllerTemplateRevision = checked((int)model.Source.ControllerTemplateRevision),
            NodeIndices = model.Slots,
            Schedule = model.Schedule,
            MaximumWorkPerScan = checked((uint)model.Instructions.Count),
            WorkingBytes = workingBytes,
            MaximumSnapshotBytes = 16384,
            SectionCount = checked((uint)sections.Length),
            InstructionCount = checked((uint)model.Instructions.Count),
            SlotCount = checked((uint)(model.Schedule.Count + model.StateIds.Length)),
            PointCount = checked((uint)model.Points.Length),
            StateCount = checked((uint)model.StateIds.Length)
        };
    }

    /*
     * Prepare the deterministic, logical compiler model used by the binary encoders.
     * No final section bytes are produced here. This stage resolves schedule order,
     * slot numbers, state storage, point bindings, constants and VM instructions so
     * all later encoders consume already-resolved numeric references.
     */
    private static CompilationModel PrepareCompilationModel(FlowCompilationRequest request)
    {
        var source = request.Source;

        // Get graph schedule.
        var schedule = GetSchedule(source);

        // Build a dictionary of node ID -> node for fast lookup during instruction encoding.
        var nodes = source.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var slots = BuildTransientSlots(schedule);
        var memoryIds = GetMemoryNodeIds(schedule, nodes);
        var stateIds = GetStateNodeIds(schedule, nodes);
        var stateSlots = BuildStateSlots(schedule.Count, stateIds);
        var points = BuildPoints(source, [.. schedule.Select(id => nodes[id])], request.Target.Points);
        var constants = BuildConstantPool(source);
        var instructions = BuildInstructions(
            source,
            schedule,
            nodes,
            slots,
            memoryIds,
            stateSlots,
            points,
            constants);

        return new CompilationModel(
            source,
            schedule,
            nodes,
            slots,
            memoryIds,
            stateIds,
            stateSlots,
            points,
            constants,
            instructions);
    }

    /*
     * Transient slot allocation is positional: scheduled node 0 writes slot 0,
     * scheduled node 1 writes slot 1, and so on. Because the schedule is
     * deterministic, these slot numbers are deterministic too. The ushort cast
     * also documents that operands/results are encoded as 16-bit slot indices.
     */
    private static Dictionary<string, ushort> BuildTransientSlots(IReadOnlyList<string> schedule)
    {
        return schedule
            .Select((id, index) => new
            {
                id,
                index = checked((ushort)index)
            })
            .ToDictionary(
                item => item.id,
                item => item.index,
                StringComparer.Ordinal);
    }

    /*
     * STATEFUL NODE TRACKING
     * ======================
     *
     * Most nodes only need a transient result slot while the current scan is
     * executing. Stateful nodes are different: they also need information that
     * survives from one scan to the next.
     *
     *
     * MEMORY NODES
     * ------------
     *
     * Keep a separate list of Memory nodes because they require additional
     * processing later in compilation.
     *
     * A Memory node exposes its previously committed state during the current
     * scan, while the value connected to its input is staged for use as state
     * on a subsequent scan.
     *
     * Memory nodes therefore require an additional MemoryCommit instruction
     * after the normal scheduled instructions.
     *
     * Conceptually:
     *
     *                         scan N
     *
     *       previous state
     *             |
     *             v
     *        +----------+
     *        |  Memory  |------> current result
     *        +----------+
     *             ^
     *             |
     *          new input
     *             |
     *             v
     *       MemoryCommit
     *             |
     *             v
     *       next state
     *
     * memoryIds is used to identify the Memory nodes that require this additional
     * state/commit processing.
     */
    private static string[] GetMemoryNodeIds(
        IEnumerable<string> schedule,
        Dictionary<string, ExecutableFlowNode> nodes)
    {
        return [.. schedule.Where(id => nodes[id].Kind == FlowNodeKind.Memory)];
    }

    /*
     * OTHER STATEFUL NODES
     * --------------------
     *
     * Identify scheduled nodes that require persistent state in addition to their
     * normal transient result slot.
     *
     * These nodes need to remember information between scans. For example:
     *
     *     OnDelay / Delay / Timer
     *         need timer state so timing can continue across scans.
     *
     *     RisingEdge / Pulse
     *         need previous-value/event state so a change can be detected on a
     *         later scan.
     *
     * Compare this with a normal stateless operation:
     *
     *     Add
     *
     *         slot[A] ----\
     *                      ADD ----> transient result
     *         slot[B] ----/
     *
     * The Add result is calculated from the current scan's inputs and does not
     * itself require persistent state.
     *
     * A stateful operation instead has both:
     *
     *                         +------------------+
     *       current input --->| Stateful node    |---> transient result
     *                         +------------------+
     *                                  ^
     *                                  |
     *                                  v
     *                              state slot
     *                                  |
     *                           survives between
     *                                scans
     */
    private static string[] GetStateNodeIds(
        IEnumerable<string> schedule,
        Dictionary<string, ExecutableFlowNode> nodes)
    {
        return [.. schedule
            .Where(id => nodes[id].Kind is
                FlowNodeKind.Memory or
                FlowNodeKind.OnDelay or
                FlowNodeKind.RisingEdge or
                FlowNodeKind.Delay or
                FlowNodeKind.Timer or
                FlowNodeKind.Pulse)
            ];
    }

    /*
     * STATE SLOT ALLOCATION
     * ---------------------
     *
     * Assign a numbered state slot to each stateful node.
     *
     * Normal scheduled-node results already occupy the first range of slot
     * indices:
     *
     *     +------------+------------------------------+
     *     | Slot       | Purpose                      |
     *     +------------+------------------------------+
     *     | 0          | scheduled node 0 result      |
     *     | 1          | scheduled node 1 result      |
     *     | 2          | scheduled node 2 result      |
     *     | ...        | ...                          |
     *     | N - 1      | scheduled node N-1 result    |
     *     +------------+------------------------------+
     *
     * State slots are placed immediately after those transient result slots:
     *
     *     schedule.Count
     *          |
     *          v
     *
     *     +------------+------------------------------+
     *     | Slot       | Purpose                      |
     *     +------------+------------------------------+
     *     | 0          | transient result             |
     *     | 1          | transient result             |
     *     | ...        | ...                          |
     *     | N - 1      | transient result             |
     *     +------------+------------------------------+
     *     | N          | stateful node 0 state        |
     *     | N + 1      | stateful node 1 state        |
     *     | N + 2      | stateful node 2 state        |
     *     +------------+------------------------------+
     *
     * Therefore:
     *
     *     transient slot index = node's position in schedule
     *
     *     state slot index     = schedule.Count
     *                            + node's position in stateIds
     *
     * Example:
     *
     *     schedule.Count = 4
     *
     *     schedule:
     *
     *         InputA       -> slot 0
     *         Timer1       -> slot 1     transient Timer1 result
     *         Not1         -> slot 2
     *         Output1      -> slot 3
     *
     *     stateIds:
     *
     *         Timer1       -> slot 4     persistent Timer1 state
     *
     * Timer1 therefore has two different storage locations:
     *
     *                 Timer1
     *                   |
     *             +-----+-----+
     *             |           |
     *             v           v
     *          slot 1      slot 4
     *         transient     state
     *          result      retained
     *                       between
     *                        scans
     *
     * The dictionary maps the source node ID to its assigned state-slot index so
     * instruction generation can efficiently obtain the correct state reference:
     *
     *     stateSlots["Timer1"] -> 4
     *
     * checked(...) ensures that allocation fails rather than silently overflowing
     * if the calculated slot index cannot fit into the 16-bit slot-index format.
     */
    private static Dictionary<string, ushort> BuildStateSlots(int transientSlotCount, IEnumerable<string> stateIds)
    {
        return stateIds
            .Select((id, index) => new
            {
                id,
                index = checked((ushort)(transientSlotCount + index))
            })
            .ToDictionary(
                item => item.id,
                item => item.index,
                StringComparer.Ordinal);
    }

    /*
     * Build a canonical constant pool before encoding instructions. Instructions
     * never embed a double directly; they carry a u16 index into this pool.
     * Sorting means equivalent resolved source produces stable pool indices and
     * therefore stable instruction bytes.
     */
    private static ConstantRecord[] BuildConstantPool(ExecutableFlowSource source)
    {
        return [.. source.Nodes
            .SelectMany(ConstantsFor)
            .Distinct()
            .OrderBy(constant => constant.DataType)
            .ThenBy(constant => constant.Number)
            ];
    }

    /*
     * Convert the deterministic node schedule into the logical VM instruction list.
     * Primary instructions are emitted in schedule order. Additional MemoryCommit
     * instructions follow, and the stream always ends with one anonymous Commit.
     */
    private static List<CompiledInstructionV1> BuildInstructions(
        ExecutableFlowSource source,
        IReadOnlyList<string> schedule,
        Dictionary<string, ExecutableFlowNode> nodes,
        Dictionary<string, ushort> slots,
        IReadOnlyList<string> memoryIds,
        Dictionary<string, ushort> stateSlots,
        IReadOnlyList<PointRecord> points,
        ConstantRecord[] constants)
    {
        // V1Instruction is still a logical C# record at this stage. Each item becomes
        // exactly 12 bytes only when EncodeV1Instruction() is called below.
        var instructions = new List<CompiledInstructionV1>();

        foreach (var id in schedule)
        {
            /*
             * Get the complete source definition for the node currently being compiled.
             *
             * 'id' comes from the deterministic execution schedule and identifies which
             * node is being converted into a VM instruction during this iteration.
             *
             * The node definition provides the information needed to determine which
             * instruction to generate, including its kind, configuration, ports, and
             * authoring metadata.
             *
             *     id
             *      |
             *      v
             *   nodes[id]
             *      |
             *      v
             * +--------------------+
             * | ExecutableFlowNode |
             * |                    |
             * | Kind               |
             * | Configuration      |
             * | Label              |
             * | position, etc.     |
             * +--------------------+
             */
            var node = nodes[id];

            /*
             * Get the numeric index of the transient slot assigned to this node's result.
             *
             * This is NOT the result value itself. It identifies the VM storage location
             * where the instruction generated for this node will write its result during
             * execution.
             *
             * Transient result slots were assigned earlier according to the node's
             * position in the deterministic execution schedule.
             *
             * For example:
             *
             *     schedule:
             *
             *         InputA   -> slot 0
             *         InputB   -> slot 1
             *         Add      -> slot 2
             *         Output   -> slot 3
             *
             * If:
             *
             *     id = "Add"
             *
             * then:
             *
             *     slots[id] = 2
             *
             * and therefore:
             *
             *     resultSlotIndex = 2
             *
             * At runtime the generated Add instruction can then perform conceptually:
             *
             *     slot[2] = slot[0] + slot[1]
             *          ^
             *          |
             *     resultSlotIndex
             *
             * The compiler stores only the slot INDEX here. The actual result VALUE is
             * produced later by the VM when the flow executes.
             */
            var resultSlotIndex = slots[id];

            instructions.Add(CreatePrimaryInstruction(
                source,
                node,
                id,
                resultSlotIndex,
                slots,
                stateSlots,
                points,
                constants));
        }

        AddMemoryCommitInstructions(source, instructions, memoryIds, slots, stateSlots);

        // The stream ends with one anonymous Commit. It has no result, operands,
        // auxiliary index, or source node. In bytes, all four u16 index fields are
        // therefore FF FF; the final reserved u16 is emitted separately as zero by
        // EncodeV1Instruction().
        instructions.Add(new CompiledInstructionV1(
            new Instruction(
                FlowOpcode.Commit,
                FlowILV1Format.Unused,
                FlowILV1Format.Unused,
                FlowILV1Format.Unused,
                FlowILV1Format.Unused),
            string.Empty,
            NodeInstructionRole.None));

        return instructions;
    }

    /*
     * MEMORY STATE UPDATE
     * ===================
     *
     * A Memory node requires two VM instructions because it must both:
     *
     *     1. Read the state that was committed by the previous scan.
     *     2. Stage a new value to become its state at the end of this scan.
     *
     * The main Memory instruction was already added above with the other
     * scheduled node instructions. It reads the Memory node's CURRENT state
     * into the node's transient result slot.
     *
     * The MemoryCommit instruction added here takes the value connected to the
     * Memory node's "in" port and stages it as the NEXT state.
     *
     * For example:
     *
     *                         current state
     *                              |
     *                              v
     *                       +--------------+
     *                       |    Memory    |------> result
     *                       +--------------+
     *                              ^
     *                              |
     *                            "in"
     *                              |
     *                              |
     *                       calculated value
     *
     * During one scan this behaves conceptually as:
     *
     *     Previous scan                                      Current scan
     *     committed state
     *           |
     *           v
     *     +-------------+
     *     | state slot  |----> Memory instruction ----> transient result
     *     +-------------+
     *           ^
     *           |
     *           |                                  new value connected
     *           |                                  to Memory."in"
     *           |                                         |
     *           |                                         v
     *           +---- commit at end of scan <---- MemoryCommit
     *
     * The important point is that MemoryCommit does NOT immediately overwrite
     * the current state while instructions are executing. The new value is
     * staged so that the state changes at the scan's commit boundary.
     *
     * This allows feedback through Memory without creating a same-scan cyclic
     * dependency:
     *
     *              +-------------------------------+
     *              |                               |
     *              v                               |
     *         +----------+       +-----+           |
     *         |  Memory  |------>| Add |-----------+
     *         +----------+       +-----+
     *
     * The Add instruction sees the Memory value from the previous committed
     * scan. Its result can then become the Memory value for the next scan.
     *
     *
     * SYMBOL ROLE
     * --------------------
     *
     * Both VM instructions originate from the same source Memory node and
     * therefore have the same NodeId. The role tells the symbol
     * metadata which generated instruction is which:
     *
     *     role = Primary      primary instruction
     *     role = Secondary    secondary instruction
     *
     *     Source Memory node
     *            |
     *            +----> Memory        (role Primary)
     *            |
     *            +----> MemoryCommit  (role Secondary)
     *
     * The MemoryCommit instructions are appended after the main scheduled
     * instructions.
     */
    private static void AddMemoryCommitInstructions(
        ExecutableFlowSource source,
        ICollection<CompiledInstructionV1> instructions,
        IEnumerable<string> memoryIds,
        Dictionary<string, ushort> slots,
        Dictionary<string, ushort> stateSlots)
    {
        foreach (var id in memoryIds)
        {
            instructions.Add(new CompiledInstructionV1(
                new Instruction(
                    FlowOpcode.MemoryCommit,
                    FlowILV1Format.Unused,
                    InputSlot(source, slots, id, "in"),
                    FlowILV1Format.Unused,
                    stateSlots[id]),
                id,
                NodeInstructionRole.Secondary));
        }
    }

    /*
     * Encode all eight Flow IL sections from the prepared logical model.
     * Individual section helpers retain the byte-layout comments beside the
     * encoding code they describe.
     */
    private static V1Section[] BuildSections(FlowCompilationRequest request, CompilationModel model)
    {
        var constantSection = BuildConstantSection(model.Constants);
        var pointSection = BuildPointSection(model.Points, request.Source.Execution.InputQualityPolicy);
        var slotSection = BuildSlotSection(model, out var slotRecordCount);
        var instructionSection = BuildInstructionSection(model.Instructions);
        var commitSection = BuildCommitSection(model, out var commitRecordCount);
        var symbolSection = BuildSymbolSection(model.Nodes, model.Instructions);
        var debugSection = BuildDebugSection(model.Instructions);
        var dependencySection = BuildDependencySection(request, model, out var dependencyRecordCount);

        // Package the eight already-encoded payloads with their IDs and logical
        // record counts. V1Section itself is not serialized directly; the loop below
        // turns this metadata into directory bytes.
        return
        [
            new(1, checked((uint)model.Constants.Length), constantSection),
            new(2, checked((uint)model.Points.Length), pointSection),
            new(3, checked((uint)slotRecordCount), slotSection),
            new(4, checked((uint)model.Instructions.Count), instructionSection),
            new(5, checked((uint)commitRecordCount), commitSection),
            new(6, checked((uint)model.Instructions.Count), symbolSection),
            new(7, checked((uint)(model.Instructions.Count - 1)), debugSection),
            new(8, checked((uint)dependencyRecordCount), dependencySection)
        ];
    }

    private static byte[] BuildConstantSection(IEnumerable<ConstantRecord> constants)
    {
        // ---------------------------------------------------------------------
        // SECTION 1 — typed constants
        // ---------------------------------------------------------------------
        // EncodeConstant emits:
        //   Boolean: [type:u8][value/flags:u8][reserved:u16]          = 4 bytes
        //   Number : [type:u8][flags=0:u8][reserved:u16][f64 LE]     = 12 bytes
        // Variable record size is safe because the type prefix tells the decoder
        // whether another eight bytes follow.
        return Concat([.. constants.Select(EncodeConstant)]);
    }

    private static byte[] BuildPointSection(
        IEnumerable<PointRecord> points,
        InputQualityPolicy qualityPolicy)
    {
        // ---------------------------------------------------------------------
        // SECTION 2 — point/interface bindings
        // ---------------------------------------------------------------------
        // Each record starts with four fixed bytes:
        //   direction:u8, dataType:u8, qualityPolicy:u8, bindingKind:u8
        // followed by two string8 values: binding id and units.
        // Because string8 is variable length, section 2 is parsed record-by-record,
        // not by multiplying count by a fixed record width.
        return Concat([.. points.Select(point => Concat(
            [
                (byte)point.Direction,
                (byte)point.DataType,
                (byte)qualityPolicy,
                (byte)point.BindingKind
            ],
            String8(point.Id),
            String8AllowEmpty(point.Units ?? string.Empty)))]);
    }

    private static byte[] BuildSlotSection(CompilationModel model, out int recordCount)
    {
        // ---------------------------------------------------------------------
        // SECTION 3 — slot layout
        // ---------------------------------------------------------------------
        // Every slot record is exactly 8 bytes:
        //   byte 0    kind
        //   byte 1    data type
        //   bytes 2-3 flags/reserved u16
        //   bytes 4-5 slot index u16
        //   bytes 6-7 initial-constant index u16
        //
        // A scheduled node result is kind=2 (transient). Transients have no initial
        // constant, so bytes 6-7 become FF FF.
        var slotRecords = model.Schedule
            .Select((id, index) => Concat(
                [2, (byte)ResultDataType(model.Source, model.Nodes[id])],
                U16(0),
                U16(index),
                U16(FlowILV1Format.Unused)))
            .ToList();

        slotRecords.AddRange(model.StateIds.Select(id => model.Nodes[id].Kind switch
        {
            FlowNodeKind.Memory => Concat(
                [3, (byte)DataType.Number],
                U16(0),
                U16(model.StateSlots[id]),
                U16(ConstantIndex(model.Constants, GetNumericConstant(model.Nodes[id], "value")))),

            FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer => Concat(
                [4, 1],
                U16(0),
                U16(model.StateSlots[id]),
                U16(ConstantIndex(model.Constants, GetNumericConstant(model.Nodes[id], "durationMs")))),

            FlowNodeKind.RisingEdge or FlowNodeKind.Pulse => Concat(
                [5, 1],
                U16(0),
                U16(model.StateSlots[id]),
                U16(ConstantIndex(model.Constants, GetBooleanConstant(false)))),

            _ => throw new UnreachableException()
        }));

        recordCount = slotRecords.Count;

        // Flatten all 8-byte records into the section payload. Concat inserts no
        // delimiter or padding between records.
        return Concat([.. slotRecords]);
    }

    private static byte[] BuildInstructionSection(IEnumerable<CompiledInstructionV1> instructions)
    {
        // ---------------------------------------------------------------------
        // SECTION 4 — scheduled instructions
        // ---------------------------------------------------------------------
        // Fixed width: instructions.Count * 12 bytes exactly.
        return Concat([.. instructions.Select(EncodeV1Instruction)]);
    }

    private static byte[] BuildCommitSection(CompilationModel model, out int recordCount)
    {
        // ---------------------------------------------------------------------
        // SECTION 5 — commit plan
        // ---------------------------------------------------------------------
        // Each record is exactly 8 bytes:
        //   kind:u8, flags:u8, target:u16, sourceSlot:u16, policy:u16
        // It describes what becomes externally/committed-state visible at the scan
        // boundary, rather than changing state/output during instruction execution.
        var commitRecords = model.MemoryIds
            .Select(id => Concat(
                [
                    (byte)FlowCommitAction.StateCommit,
                    FlowILV1Format.ReservedByte
                ],
                U16(model.StateSlots[id]),
                U16(InputSlot(model.Source, model.Slots, id, "in")),
                U16(FlowILV1Format.ReservedUInt16)))
            .ToList();

        commitRecords.AddRange(model.Schedule
            .Where(id => model.Nodes[id].Kind is
                FlowNodeKind.DigitalOutput or
                FlowNodeKind.AnalogOutput or
                FlowNodeKind.FlowOutput)
            .Select(id => Concat(
                [2, 0],
                U16(PointIndex(
                    model.Points,
                    model.Nodes[id],
                    DataDirection.Output,
                    ResultDataType(model.Source, model.Nodes[id]))),
                U16(model.Slots[id]),
                U16(0))));

        commitRecords.AddRange(model.StateIds
            .Where(id => model.Nodes[id].Kind is
                FlowNodeKind.OnDelay or
                FlowNodeKind.RisingEdge or
                FlowNodeKind.Delay or
                FlowNodeKind.Timer or
                FlowNodeKind.Pulse)
            .Select(id => Concat(
                [1, 0],
                U16(model.StateSlots[id]),
                U16(model.Slots[id]),
                U16(0))));

        recordCount = commitRecords.Count;
        return Concat([.. commitRecords]);
    }

    private static byte[] BuildSymbolSection(
        Dictionary<string, ExecutableFlowNode> nodes,
        IEnumerable<CompiledInstructionV1> instructions)
    {
        // ---------------------------------------------------------------------
        // SECTION 6 — symbols / authoring recovery metadata
        // ---------------------------------------------------------------------
        // One symbol record is emitted for every instruction, including Commit.
        // Record shape:
        //   instructionIndex:u16
        //   role:u8
        //   nodeId:string8 (empty for anonymous Commit)
        //   label:string8
        //   x:f64 LE
        //   y:f64 LE
        //   zOrder:f64 LE
        //   groupId:string8
        //
        // The section is variable-width because the strings are variable-width.
        // The three doubles consume 24 bytes and preserve designer placement.
        return Concat([.. instructions.Select((instruction, index) =>
        {
            var authoring = instruction.NodeId.Length == 0 ? null : nodes[instruction.NodeId];

            return Concat(
                U16(index),
                [(byte)instruction.Role],
                String8AllowEmpty(instruction.NodeId),
                String8AllowEmpty(authoring is null ? string.Empty : AuthoringLabel(authoring)),
                F64(authoring?.X ?? 0D),
                F64(authoring?.Y ?? 0D),
                F64(authoring?.ZOrder ?? 0D),
                String8AllowEmpty(authoring?.GroupId ?? string.Empty));
        })]);
    }

    private static byte[] BuildDebugSection(IEnumerable<CompiledInstructionV1> instructions)
    {
        // ---------------------------------------------------------------------
        // SECTION 7 — debug map
        // ---------------------------------------------------------------------
        // Each entry is:
        //   instructionIndex:u16, resultSlot:u16, nodeId:string8
        // Anonymous instructions (notably the final Commit) do not get a debug entry.
        //
        // Note that Select's 'index' here is the index within the filtered sequence
        // as written by this implementation; this comment deliberately documents the
        // code rather than changing its indexing policy.
        return Concat([.. instructions
            .Where(compiledInstruction => compiledInstruction.NodeId.Length > 0)
            .Select((compiledInstruction, index) => Concat(
                U16(index),
                U16(compiledInstruction.Instruction.ResultSlotIndex),
                String8(compiledInstruction.NodeId)))]);
    }

    private static byte[] BuildDependencySection(
        FlowCompilationRequest request,
        CompilationModel model,
        out int recordCount)
    {
        var resolvedPoints = request.Target.Points
            .GroupBy(point => point.Id, StringComparer.Ordinal)
            .Select(group => group.Single())
            .OrderBy(point => point.Id, StringComparer.Ordinal)
            .ToArray();

        // ---------------------------------------------------------------------
        // SECTION 8 — source dependencies
        // ---------------------------------------------------------------------
        // Variable-width record:
        //   kind:u8, dependencyId:string8, revision:u32 LE
        // kind 1 is the controller template; point dependencies are emitted below.
        var dependencyRecords = new List<byte[]>
        {
            Concat(
                [(byte)FlowDependencyKind.ControllerTemplate],
                String8(model.Source.ControllerTemplateId),
                U32(model.Source.ControllerTemplateRevision))
        };

        dependencyRecords.AddRange(model.Points
            .Where(point => point.BindingKind == PointBindingKind.ControllerPoint)
            .Select(point => point.Id)
            .Distinct(StringComparer.Ordinal)
            .Select(pointId =>
            {
                var resolved = resolvedPoints.SingleOrDefault(candidate => candidate.Id == pointId)
                    ?? throw Failure(FlowCompilationDiagnosticCode.MissingPoint, $"/points/{Escape(pointId)}", pointId);

                var revision = resolved.Revision;

                if (revision <= 0)
                {
                    throw Failure(FlowCompilationDiagnosticCode.InvalidDependencyRevision, $"/points/{Escape(pointId)}/revision");
                }

                return Concat([2], String8(pointId), U32(checked((uint)revision)));
            }));

        recordCount = dependencyRecords.Count;
        return Concat([.. dependencyRecords]);
    }

    private static List<byte[]> BuildSectionDirectory(V1Section[] sections, out uint artifactLength)
    {
        // Every section-directory record is:
        //   0..1   section id        u16 LE
        //   2..3   section version   u16 LE
        //   4..7   payload offset    u32 LE, absolute from artifact byte 0
        //   8..11  payload length    u32 LE
        //   12..15 record count      u32 LE
        //   16..47 SHA-256           32 raw digest bytes
        //
        // First section payload begins immediately after:
        //   128-byte envelope + 8 * 48-byte directory = byte offset 512.
        // 'offset' always means an absolute byte position in the final artifact.
        var offset = checked((uint)(FlowILV1Format.EnvelopeLength + (sections.Length * FlowILV1Format.DirectoryEntryLength)));
        var directory = new List<byte[]>();

        foreach (var section in sections)
        {
            // Build one exactly-48-byte directory record. SHA256.HashData returns
            // 32 raw digest bytes; they are stored directly, not as 64 hex chars.
            directory.Add(Concat(
                U16(section.Id),
                U16(section.Version),
                U32(offset),
                U32(checked((uint)section.Bytes.Length)),
                U32(section.Count),
                SHA256.HashData(section.Bytes)));

            // Advance to the next section's absolute start. Since sections are
            // concatenated with no gaps, this also guarantees contiguous ranges.
            offset += checked((uint)section.Bytes.Length);
        }

        // After the loop, offset is no longer a section start: it is exactly the
        // computed final artifact length.
        if (offset > FlowILV1Format.MaximumArtifactBytes)
        {
            throw Failure(FlowCompilationDiagnosticCode.ArtifactSizeLimitExceeded, "/artifactLength", 16384);
        }

        artifactLength = offset;
        return directory;
    }

    private static FlowILCapability DetermineRequiredCapabilities(
        ExecutableFlowSource source,
        IReadOnlyCollection<PointRecord> points,
        string[] memoryIds)
    {
        var capabilities =
            FlowILCapability.Base |
            FlowILCapability.Boolean;

        if (points.Any(point => point.Direction == DataDirection.Input))
        {
            capabilities |= FlowILCapability.Inputs;
        }

        if (points.Any(point => point.Direction == DataDirection.Output))
        {
            capabilities |= FlowILCapability.Outputs;
        }

        if (memoryIds.Length > 0)
        {
            capabilities |= FlowILCapability.State;
        }

        if (source.Nodes.Any(node =>
            node.Kind is
                FlowNodeKind.Nand or
                FlowNodeKind.Nor or
                FlowNodeKind.Xor or
                FlowNodeKind.Xnor))
        {
            capabilities |= FlowILCapability.ExpandedBoolean;
        }

        if (source.Nodes.Any(node =>
                node.Kind is
                    FlowNodeKind.NumericConstant or
                    FlowNodeKind.Add or
                    FlowNodeKind.Comparator or
                    FlowNodeKind.LevelShifter or
                    FlowNodeKind.Average or
                    FlowNodeKind.Calculator or
                    FlowNodeKind.Clamp or
                    FlowNodeKind.Min or
                    FlowNodeKind.Max or
                    FlowNodeKind.Line or
                    FlowNodeKind.Selector)
            || points.Any(point => point.DataType == DataType.Number))
        {
            capabilities |= FlowILCapability.Numeric;
        }

        if (source.Nodes.Any(node => node.Kind == FlowNodeKind.Comparator))
        {
            capabilities |= FlowILCapability.Comparison;
        }

        if (source.Nodes.Any(node => node.Kind == FlowNodeKind.LevelShifter))
        {
            capabilities |= FlowILCapability.LevelShifter;
        }

        if (source.Nodes.Any(node => node.Kind == FlowNodeKind.QualityGood))
        {
            capabilities |= FlowILCapability.Quality;
        }

        if (source.Nodes.Any(node => node.Kind is FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer))
        {
            capabilities |= FlowILCapability.Timer;
        }

        if (source.Nodes.Any(node => node.Kind is FlowNodeKind.RisingEdge or FlowNodeKind.Pulse))
        {
            capabilities |= FlowILCapability.Event;
        }

        return capabilities;
    }

    private static byte[] BuildEnvelope(
        ExecutableFlowSource source,
        int sectionCount,
        int instructionCount,
        uint artifactLength,
        FlowILCapability capabilities,
        uint workingBytes)
    {
        // ---------------------------------------------------------------------
        // FIXED 128-BYTE ENVELOPE
        // ---------------------------------------------------------------------
        // New byte[] is zero-initialized, which is important: every reserved byte
        // that is not explicitly written below remains canonical zero.
        var envelope = new byte[FlowILV1Format.EnvelopeLength];

        // bytes 0..3: ASCII magic 46 49 4C 31 ("FIL1").
        "FIL1"u8.CopyTo(envelope);
        // bytes 4..5   : IL version, u16 LE.
        WriteU16(envelope, 4, 1);
        // bytes 6..7   : envelope length = 128, u16 LE.
        WriteU16(envelope, 6, FlowILV1Format.EnvelopeLength);
        // bytes 8..11  : exact final artifact length, u32 LE.
        WriteU32(envelope, 8, artifactLength);
        // bytes 12..15 : flags. This implementation writes bit 0 = 1.
        WriteU32(envelope, 12, 1);
        // bytes 16..19 : flow revision.
        WriteU32(envelope, 16, source.Revision);
        // bytes 20..23 : resolved controller-template revision.
        WriteU32(envelope, 20, source.ControllerTemplateRevision);
        // bytes 24..25 : minimum host ABI.
        WriteU16(envelope, 24, 1);
        // bytes 26..27 : section count (8).
        WriteU16(envelope, 26, sectionCount);
        // byte 28      : input-quality policy. bytes 29..31 stay zero.
        envelope[28] = (byte)source.Execution.InputQualityPolicy;
        // bytes 32..35 : bounded maximum work per scan.
        WriteU32(envelope, 32, checked((uint)instructionCount));
        // bytes 36..43 : required-capability bitmap, u64 LE.
        BinaryPrimitives.WriteUInt64LittleEndian(envelope.AsSpan(36), (ulong)capabilities);
        // bytes 44..47 : VM working-byte estimate.
        WriteU32(envelope, 44, workingBytes);
        // bytes 48..51 : maximum snapshot bytes.
        WriteU32(envelope, 48, 16384);
        // bytes 52..115: one-byte flow-ID byte length + UTF-8 ID + zero padding.
        WritePaddedIdentifier(envelope, 52, source.Id);
        // bytes 116..119: directory absolute offset (= 128).
        // bytes 120..127 remain zero from array initialization.
        WriteU32(envelope, 116, FlowILV1Format.EnvelopeLength);

        return envelope;
    }

    private static List<string> GetSchedule(ExecutableFlowSource source)
    {
        /*
         * Build a lookup table containing every node in the flow. 
         * Perform a deterministic Kahn topological sort of the flow graph,
         * ignoring incoming edges to Memory nodes.
         *
         * Key:
         *     node.Id
         *
         * Value:
         *     the actual ExecutableFlowNode
         *
         * Example:
         *
         *     "input1"  -> DigitalInput node
         *     "and1"    -> And node
         *     "output1" -> DigitalOutput node
         *
         * We use StringComparer.Ordinal so node IDs are compared exactly and
         * deterministically.
         */
        var nodes = source.Nodes.ToDictionary(
            node => node.Id,
            StringComparer.Ordinal);

        /*
         * "indegree" means:
         *
         *     HOW MANY OTHER NODES MUST RUN BEFORE THIS NODE CAN RUN?
         *
         * More formally, it is the number of incoming dependency edges.
         *
         * We initially set every node's indegree to zero because we have not
         * examined the connections yet.
         *
         * Example flow:
         *
         *     A ----\
         *            AND ----> D
         *     B ----/
         *
         * Initially:
         *
         *     A   = 0
         *     B   = 0
         *     AND = 0
         *     D   = 0
         *
         * After examining the connections:
         *
         *     A   = 0
         *     B   = 0
         *     AND = 2     // waits for A and B
         *     D   = 1     // waits for AND
         */
        var indegree = nodes.Keys.ToDictionary(
            id => id,
            _ => 0,
            StringComparer.Ordinal);

        /*
         * For each node, keep a list of nodes that depend on it.
         *
         * Another way to describe this:
         *
         *     outgoing[A]
         *
         * means:
         *
         *     "Which nodes become closer to being ready after A executes?"
         *
         * For:
         *
         *     A ---> C
         *     A ---> D
         *
         * we would eventually have:
         *
         *     outgoing["A"] = ["C", "D"]
         *
         * Initially every list is empty because we have not examined the
         * connections yet.
         */
        var outgoing = nodes.Keys.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);

        /*
         * Examine every connection in the designer graph and turn it into
         * scheduling dependency information.
         */
        foreach (var connection in source.Connections)
        {
            /*
             * MEMORY IS SPECIAL.
             *
             * If the connection goes INTO a Memory node, that connection is not
             * treated as a dependency for the current execution schedule.
             *
             * Why?
             *
             * Memory represents state carried between scans/ticks. Its current
             * value can be read before its new input value has been calculated.
             *
             * The new input to Memory is committed separately after the main
             * scheduled instructions.
             *
             * Therefore an edge into Memory must NOT force Memory to wait for
             * the node feeding its "in" port.
             *
             * This is also important because Memory is what allows a logical
             * feedback loop without producing a scheduling cycle.
             *
             * Example:
             *
             *          +----------+
             *          |          v
             *     Memory ---> Add
             *        ^         |
             *        |         |
             *        +---------+
             *
             * If the Add -> Memory connection were treated as an ordinary
             * scheduling dependency, this would appear circular:
             *
             *     Memory waits for Add
             *     Add waits for Memory
             *
             * By ignoring dependencies INTO Memory, Memory can supply its
             * previously committed value first.
             */
            if (nodes[connection.Target.NodeId].Kind == FlowNodeKind.Memory)
            {
                continue;
            }

            /*
             * The target node has one more prerequisite.
             *
             * Example:
             *
             *     A ---> C
             *
             * means C cannot execute until A has executed.
             *
             * Therefore:
             *
             *     indegree["C"]++
             *
             * If another connection exists:
             *
             *     B ---> C
             *
             * then C's indegree becomes 2.
             *
             * C now has to wait for both A and B.
             */
            indegree[connection.Target.NodeId]++;

            /*
             * Record the dependency in the other direction as well.
             *
             * If:
             *
             *     A ---> C
             *
             * then C depends on A, so:
             *
             *     outgoing["A"].Add("C")
             *
             * Later, when A has been scheduled, we can look at outgoing["A"]
             * and reduce the number of prerequisites remaining for C.
             */
            outgoing[connection.Source.NodeId]
                .Add(connection.Target.NodeId);
        }

        /*
         * Find every node that currently has ZERO prerequisites.
         *
         * These nodes can execute immediately.
         *
         * Example:
         *
         *     InputA ----\
         *                 Add ---> Output
         *     InputB ----/
         *
         * indegree:
         *
         *     InputA = 0
         *     InputB = 0
         *     Add    = 2
         *     Output = 1
         *
         * So initially:
         *
         *     ready = { InputA, InputB }
         *
         *
         * SortedSet is important here.
         *
         * There may be several nodes that are ready at the same time. Their
         * execution order usually would not affect the logical result, but it
         * WOULD affect the generated binary because slot numbers and instruction
         * positions depend on scheduling order.
         *
         * SortedSet + StringComparer.Ordinal means:
         *
         *     always choose the alphabetically/ordinally smallest node ID.
         *
         * This makes compilation deterministic.
         */
        var ready = new SortedSet<string>(
            indegree
                .Where(item => item.Value == 0)
                .Select(item => item.Key),
            StringComparer.Ordinal);

        /*
         * This becomes the final execution order.
         *
         * Example result:
         *
         *     [
         *         "InputA",
         *         "InputB",
         *         "Add",
         *         "Output"
         *     ]
         */
        var result = new List<string>(nodes.Count);

        /*
         * Continue until there are no nodes currently able to execute.
         */
        while (ready.Count > 0)
        {
            /*
             * Pick the first node from the sorted ready set.
             *
             * Because ready is a SortedSet, ready.Min is deterministic.
             */
            var id = ready.Min!;

            /*
             * Remove it from the ready set because we are about to schedule it.
             */
            ready.Remove(id);

            /*
             * Add this node to the final execution schedule.
             *
             * Conceptually:
             *
             *     "This node now executes."
             */
            result.Add(id);

            /*
             * Look at every node that directly depends on the node we just
             * scheduled.
             *
             * We sort this list too, again to ensure deterministic processing.
             */
            foreach (var target in outgoing[id].Order(StringComparer.Ordinal))
            {
                /*
                 * One of target's prerequisites has now been satisfied.
                 *
                 * So decrease its remaining prerequisite count.
                 *
                 * Example:
                 *
                 * Before A executes:
                 *
                 *     indegree["AND"] = 2
                 *
                 * A executes:
                 *
                 *     indegree["AND"] = 1
                 *
                 * B executes:
                 *
                 *     indegree["AND"] = 0
                 *
                 * At zero, AND is ready to execute.
                 */
                if (--indegree[target] == 0)
                {
                    /*
                     * The target has no remaining prerequisites.
                     *
                     * Put it into the ready set so it can now be scheduled.
                     */
                    ready.Add(target);
                }
            }
        }

        /*
         * Every node must appear in the completed schedule.
         *
         * If fewer nodes were scheduled than exist in the graph, some nodes never
         * reached an indegree of zero. This means they are still waiting on each
         * other through a cyclic dependency and therefore cannot be given a valid
         * execution order.
         * 
         * Example cyclic dependency:
         * 
         *     InputA ----\
         *                 Add ---> Multiply ---> Output
         *            +---/                        |
         *            |                            |
         *            +----------------------------+
         */
        if (result.Count != nodes.Count)
        {
            throw Failure(FlowCompilationDiagnosticCode.CyclicDependency, "/connections");
        }

        /*
         * Return the deterministic list of node IDs in execution order.
         */
        return result;
    }

    /*
 * Create the primary VM instruction for one scheduled source node.
 *
 * Each source node produces one primary instruction. Some stateful nodes may
 * also produce additional instructions later in compilation.
 *
 * The instruction contains numeric references to transient slots, state slots,
 * constants, and point bindings that have already been allocated by the
 * compiler.
 *
 * The fields correspond to the eventual 12-byte instruction record:
 *
 *     +------------+---------------------------------------------+
 *     | Field      | Purpose                                     |
 *     +------------+---------------------------------------------+
 *     | Opcode     | Operation the VM will perform               |
 *     | Result     | Transient slot index to receive the result  |
 *     | Operand0   | First input slot/index, when required       |
 *     | Operand1   | Second input slot/index, when required      |
 *     | Auxiliary  | Additional slot/index/code, when required   |
 *     +------------+---------------------------------------------+
 *
 * NodeId and Role are compiler metadata used when building symbols/debug
 * information; they are not encoded into the instruction record itself.
 *
 * FlowILV1Format.Unused (0xFFFF) represents an unused slot/index field.
 */
    private static CompiledInstructionV1 CreatePrimaryInstruction(
        ExecutableFlowSource source,
        ExecutableFlowNode node,
        string nodeId,
        ushort resultSlotIndex,
        Dictionary<string, ushort> slots,
        Dictionary<string, ushort> stateSlots,
        IReadOnlyList<PointRecord> points,
        ConstantRecord[] constants)
    {
        // The common logical fields are:
        //   Opcode        -> byte 0 of the final 12-byte record
        //   Result        -> bytes 2..3
        //   Operand0      -> bytes 4..5
        //   Operand1      -> bytes 6..7
        //   Auxiliary     -> bytes 8..9
        //   NodeId        -> NOT encoded in section 4; used to build symbols/debug
        //   Role          -> NOT encoded in section 4; stored in symbol metadata
        //
        // FlowILV1Format.Unused (0xFFFF) is used wherever an index field is unused.
        var context = new InstructionCreationContext(
            source,
            slots,
            stateSlots,
            points,
            constants,
            nodeId,
            resultSlotIndex);

        return node.Kind switch
        {
            FlowNodeKind.DigitalInput or
            FlowNodeKind.AnalogInput or
            FlowNodeKind.FlowInput or
            FlowNodeKind.DigitalConstant or
            FlowNodeKind.NumericConstant or
            FlowNodeKind.Schedule or
            FlowNodeKind.Calendar => CreateSourceInstruction(context, node),

            FlowNodeKind.Not or
            FlowNodeKind.And or
            FlowNodeKind.Or or
            FlowNodeKind.Nand or
            FlowNodeKind.Nor or
            FlowNodeKind.Xor or
            FlowNodeKind.Xnor => CreateBooleanInstruction(context, node),

            FlowNodeKind.Add or
            FlowNodeKind.Comparator or
            FlowNodeKind.LevelShifter or
            FlowNodeKind.QualityGood or
            FlowNodeKind.Average or
            FlowNodeKind.Calculator or
            FlowNodeKind.Split or
            FlowNodeKind.Override or
            FlowNodeKind.Min or
            FlowNodeKind.Max or
            FlowNodeKind.Clamp or
            FlowNodeKind.Line or
            FlowNodeKind.If or
            FlowNodeKind.Selector or
            FlowNodeKind.Sequence => CreateCalculationInstruction(context, node),

            FlowNodeKind.OnDelay or
            FlowNodeKind.RisingEdge or
            FlowNodeKind.Memory or
            FlowNodeKind.Delay or
            FlowNodeKind.Timer or
            FlowNodeKind.Pulse => CreateStatefulInstruction(context, node),

            FlowNodeKind.DigitalOutput or
            FlowNodeKind.AnalogOutput or
            FlowNodeKind.FlowOutput => CreateOutputInstruction(context, node),

            _ => throw new UnreachableException()
        };
    }

    /*
     * Create instructions whose value originates from an external binding or a
     * configured literal rather than from another transient result slot.
     */
    private static CompiledInstructionV1 CreateSourceInstruction(
        InstructionCreationContext context,
        ExecutableFlowNode node)
    {
        return node.Kind switch
        {
            FlowNodeKind.DigitalInput =>
                new(
                    new(
                        FlowOpcode.PointInput,
                        context.ResultSlotIndex,
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused,
                        PointIndex(context.Points, node, DataDirection.Input, DataType.Boolean)
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.AnalogInput =>
                new(
                    new(
                        FlowOpcode.PointInput,
                        context.ResultSlotIndex,
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused,
                        PointIndex(context.Points, node, DataDirection.Input, DataType.Number)
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.FlowInput =>
                new(
                    new(
                        FlowOpcode.PointInput,
                        context.ResultSlotIndex,
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused,
                        PointIndex(
                            context.Points,
                            node,
                            DataDirection.Input,
                            InterfaceDataType(context.Source, node))
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.DigitalConstant =>
                new(
                    new(
                        FlowOpcode.DigitalConstant,
                        context.ResultSlotIndex,
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused,
                        ConstantIndex(
                            context.Constants,
                            GetBooleanConstant(node.Configuration["value"].GetBoolean()))
                        ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.NumericConstant =>
                new(
                    new(
                        FlowOpcode.NumericConstant,
                        context.ResultSlotIndex,
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused,
                        ConstantIndex(context.Constants, GetNumericConstant(node, "value"))
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Schedule or FlowNodeKind.Calendar =>
                new(
                    new(
                        FlowOpcode.DigitalConstant,
                        context.ResultSlotIndex,
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused,
                        ConstantIndex(
                            context.Constants,
                            GetBooleanConstant(node.Configuration["enabled"].GetBoolean()))
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            _ => throw new UnreachableException()
        };
    }

    /*
     * Create the stateless Boolean combinator instructions. These instructions read
     * only transient input slots and do not reference persistent state.
     */
    private static CompiledInstructionV1 CreateBooleanInstruction(
        InstructionCreationContext context,
        ExecutableFlowNode node)
    {
        return node.Kind switch
        {
            FlowNodeKind.Not =>
                new(
                    new(
                        FlowOpcode.Not,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "in"),
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.And => CreateBinaryBooleanInstruction(context, FlowOpcode.And),
            FlowNodeKind.Or => CreateBinaryBooleanInstruction(context, FlowOpcode.Or),
            FlowNodeKind.Nand => CreateBinaryBooleanInstruction(context, FlowOpcode.Nand),
            FlowNodeKind.Nor => CreateBinaryBooleanInstruction(context, FlowOpcode.Nor),
            FlowNodeKind.Xor => CreateBinaryBooleanInstruction(context, FlowOpcode.Xor),
            FlowNodeKind.Xnor => CreateBinaryBooleanInstruction(context, FlowOpcode.Xnor),
            _ => throw new UnreachableException()
        };
    }

    private static CompiledInstructionV1 CreateBinaryBooleanInstruction(
        InstructionCreationContext context,
        FlowOpcode opcode)
    {
        return new(
            new(
                opcode,
                context.ResultSlotIndex,
                InputSlot(context.Source, context.Slots, context.NodeId, "a"),
                InputSlot(context.Source, context.Slots, context.NodeId, "b"),
                FlowILV1Format.Unused
            ),
            context.NodeId,
            NodeInstructionRole.Primary);
    }

    /*
     * Create numeric, selection and passthrough instructions. Although these node
     * kinds perform different operations, they are all stateless at the VM level and
     * therefore need only transient input/result slots plus optional constants.
     */
    private static CompiledInstructionV1 CreateCalculationInstruction(
        InstructionCreationContext context,
        ExecutableFlowNode node)
    {
        return node.Kind switch
        {
            FlowNodeKind.Add =>
                new(
                    new(
                        FlowOpcode.Add,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "a"),
                        InputSlot(context.Source, context.Slots, context.NodeId, "b"),
                        FlowILV1Format.Unused
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Comparator =>
                new(
                    new(
                        FlowOpcode.Comparator,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "a"),
                        InputSlot(context.Source, context.Slots, context.NodeId, "b"),
                        ComparatorCode(node)
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.LevelShifter =>
                new(
                    new(
                        FlowOpcode.LevelShifter,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "in"),
                        ConstantIndex(context.Constants, GetNumericConstant(node, "gain")),
                        ConstantIndex(context.Constants, GetNumericConstant(node, "offset"))
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.QualityGood =>
                new(
                    new(
                        FlowOpcode.QualityGood,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "in"),
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Average or
            FlowNodeKind.Calculator or
            FlowNodeKind.Split or
            FlowNodeKind.Override =>
                new(
                    new(
                        FlowOpcode.Passthrough,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "input"),
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Min => CreateBinaryNumericInstruction(context, FlowOpcode.Min),
            FlowNodeKind.Max => CreateBinaryNumericInstruction(context, FlowOpcode.Max),

            FlowNodeKind.Clamp =>
                new(
                    new(
                        FlowOpcode.Clamp,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "input"),
                        ConstantIndex(context.Constants, GetNumericConstant(node, "minimum")),
                        ConstantIndex(context.Constants, GetNumericConstant(node, "maximum"))
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Line =>
                new(
                    new(
                        FlowOpcode.Line,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "input"),
                        ConstantIndex(context.Constants, GetNumericConstant(node, "gain")),
                        ConstantIndex(context.Constants, GetNumericConstant(node, "offset"))
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.If =>
                new(
                    new(
                        FlowOpcode.If,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "condition"),
                        InputSlot(context.Source, context.Slots, context.NodeId, "whenTrue"),
                        InputSlot(context.Source, context.Slots, context.NodeId, "whenFalse")
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Selector =>
                new(
                    new(
                        FlowOpcode.Selector,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "condition"),
                        InputSlot(context.Source, context.Slots, context.NodeId, "a"),
                        InputSlot(context.Source, context.Slots, context.NodeId, "b")
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Sequence => CreateBinaryNumericInstruction(context, FlowOpcode.Sequence),
            _ => throw new UnreachableException()
        };
    }

    private static CompiledInstructionV1 CreateBinaryNumericInstruction(
        InstructionCreationContext context,
        FlowOpcode opcode)
    {
        return new(
            new(
                opcode,
                context.ResultSlotIndex,
                InputSlot(context.Source, context.Slots, context.NodeId, "a"),
                InputSlot(context.Source, context.Slots, context.NodeId, "b"),
                FlowILV1Format.Unused
            ),
            context.NodeId,
            NodeInstructionRole.Primary);
    }

    /*
     * Create instructions that depend on persistent state in addition to their
     * transient result slot. The auxiliary field identifies the state slot allocated
     * for this source node.
     */
    private static CompiledInstructionV1 CreateStatefulInstruction(
        InstructionCreationContext context,
        ExecutableFlowNode node)
    {
        return node.Kind switch
        {
            FlowNodeKind.OnDelay =>
                new(
                    new(
                        FlowOpcode.OnDelay,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "in"),
                        FlowILV1Format.Unused,
                        context.StateSlots[context.NodeId]
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.RisingEdge =>
                new(
                    new(
                        FlowOpcode.RisingEdge,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "in"),
                        FlowILV1Format.Unused,
                        context.StateSlots[context.NodeId]
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Memory =>
                new(
                    new(
                        FlowOpcode.Memory,
                        context.ResultSlotIndex,
                        FlowILV1Format.Unused,
                        FlowILV1Format.Unused,
                        context.StateSlots[context.NodeId]
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Delay or FlowNodeKind.Timer =>
                new(
                    new(
                        FlowOpcode.OnDelay,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "input"),
                        FlowILV1Format.Unused,
                        context.StateSlots[context.NodeId]
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Pulse =>
                new(
                    new(
                        FlowOpcode.RisingEdge,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "input"),
                        FlowILV1Format.Unused,
                        context.StateSlots[context.NodeId]
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            _ => throw new UnreachableException()
        };
    }

    /*
     * Create proposed-output instructions. The VM writes the node's transient result
     * slot during execution, while the auxiliary field identifies the binding that is
     * later published by the commit plan.
     */
    private static CompiledInstructionV1 CreateOutputInstruction(
        InstructionCreationContext context,
        ExecutableFlowNode node)
    {
        return node.Kind switch
        {
            FlowNodeKind.DigitalOutput =>
                new(
                    new(
                        FlowOpcode.PointOutput,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "in"),
                        FlowILV1Format.Unused,
                        PointIndex(context.Points, node, DataDirection.Output, DataType.Boolean)
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.AnalogOutput =>
                new(
                    new(
                        FlowOpcode.PointOutput,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "in"),
                        FlowILV1Format.Unused,
                        PointIndex(context.Points, node, DataDirection.Output, DataType.Number)
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.FlowOutput =>
                new(
                    new(
                        FlowOpcode.PointOutput,
                        context.ResultSlotIndex,
                        InputSlot(context.Source, context.Slots, context.NodeId, "value"),
                        FlowILV1Format.Unused,
                        PointIndex(
                            context.Points,
                            node,
                            DataDirection.Output,
                            InterfaceDataType(context.Source, node))
                    ),
                    context.NodeId,
                    NodeInstructionRole.Primary),

            _ => throw new UnreachableException()
        };
    }

    /*
     * Resolve one input port to the transient slot containing the value that
     * drives it.
     *
     * Designer connections name nodes and ports:
     *
     *     Add.value ----> Output.in
     *
     * VM instructions do not follow those names at runtime. They read numbered
     * slots, so this method translates the target port back through its single
     * incoming connection and returns the source node's allocated slot index:
     *
     *     target node/port
     *           |
     *           v
     *     matching connection
     *           |
     *           v
     *       source node
     *           |
     *           v
     *     slots[sourceId]
     *
     * Validation has already guaranteed that each required input has exactly
     * one driver, which is why Single() is appropriate here.
     */
    private static ushort InputSlot(
        ExecutableFlowSource source,
        Dictionary<string, ushort> slots,
        string targetId,
        string portId) => slots[source.Connections.Single(connection =>
            connection.Target.NodeId == targetId && connection.Target.PortId == portId).Source.NodeId];

    /*
     * Return the numeric index of the canonical point/interface record used by
     * this node. Instructions store this compact index rather than a point ID.
     *
     * Physical I/O nodes identify a binding with configuration["pointId"], while
     * FlowInput/FlowOutput nodes use configuration["interfaceId"]. The lookup
     * also includes direction, data type, and node kind so two records with the
     * same textual ID cannot be confused when they represent different bindings.
     *
     *     node configuration ID
     *              |
     *              v
     *       canonical points[]
     *              |
     *              v
     *       zero-based index
     *              |
     *              v
     *       instruction field
     */
    private static ushort PointIndex(IReadOnlyList<PointRecord> points, ExecutableFlowNode node, DataDirection direction, DataType type)
    {
        var pointId = node.Configuration[node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput ? "interfaceId" : "pointId"].GetString();

        return checked((ushort)points.Select((point, index) => new { point, index })
            .Single(item =>
                item.point.Id == pointId &&
                item.point.Direction == direction &&
                item.point.DataType == type &&
                item.point.Kind == node.Kind).index
            );
    }

    /*
     * Serialize one scheduled VM instruction to EXACTLY 12 bytes:
     *
     *   byte  offset  size   meaning
     *   ----  ------  ----   ---------------------------------------------
     *          0       1     opcode
     *          1       1     flags (always zero here)
     *          2       2     result slot, little-endian u16
     *          4       2     operand 0 slot/index, little-endian u16
     *          6       2     operand 1 slot/index, little-endian u16
     *          8       2     auxiliary slot/index/code, little-endian u16
     *         10       2     reserved (always zero)
     *
     * Example: an instruction whose result slot is 3 encodes that field as
     * bytes 03 00. An unused slot/index is ushort.MaxValue and therefore encodes
     * as FF FF. Concat performs raw byte concatenation, so no CLR padding exists.
     */
    private static byte[] EncodeV1Instruction(CompiledInstructionV1 compiledInstruction)
    {
        return Concat(
            [(byte)compiledInstruction.Instruction.Opcode, 0],
            U16(compiledInstruction.Instruction.ResultSlotIndex),
            U16(compiledInstruction.Instruction.Operand0),
            U16(compiledInstruction.Instruction.Operand1),
            U16(compiledInstruction.Instruction.Auxiliary),
            U16(0));
    }

    /*
     * Validate the executable graph structure before scheduling or encoding it.
     *
     * This pass establishes the assumptions used by later compiler code:
     *
     *     - every node ID is valid and unique
     *     - every node kind is supported
     *     - node configuration is valid for that kind
     *     - every connection references real ports
     *     - connections run Output -> Input
     *     - connected ports carry the same data type
     *     - each input has at most one driver
     *     - every required input has a driver
     *     - physical output points have at most one proposed-output node
     *     - the combinational portion of the graph is acyclic
     *
     * 'shapes' is built per node rather than using Shapes directly because
     * FlowInput and FlowOutput obtain their data type from the flow interface.
     * Their effective port shape therefore depends on the selected interface
     * entry.
     */
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
                throw Failure(FlowCompilationDiagnosticCode.InvalidAuthoringMetadata, $"/nodes/{index}");
            }

            if (node.GroupId is { Length: > 0 } groupId)
            {
                ValidateIdentifier(groupId, $"/nodes/{index}/groupId", 63);
            }
            if (!nodes.TryAdd(node.Id, node))
            {
                throw Failure(FlowCompilationDiagnosticCode.DuplicateNode, $"/nodes/{index}/id", node.Id);
            }

            if (!Shapes.TryGetValue(node.Kind, out var shape))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnsupportedNode, $"/nodes/{index}/kind", node.Kind);
            }

            ValidateConfiguration(source, node, index);
            shapes[node.Id] = node.Kind switch
            {
                FlowNodeKind.FlowInput => new[] { new FlowPort("value", DataDirection.Output, InterfaceDataType(source, node)) }.ToDictionary(port => port.Id, StringComparer.Ordinal),
                FlowNodeKind.FlowOutput => new[] { new FlowPort("value", DataDirection.Input, InterfaceDataType(source, node)) }.ToDictionary(port => port.Id, StringComparer.Ordinal),
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
                throw Failure(FlowCompilationDiagnosticCode.InvalidConnectionDirection, $"/connections/{index}");
            }

            if (sourcePort.DataType != targetPort.DataType)
            {
                throw Failure(FlowCompilationDiagnosticCode.ConnectionTypeMismatch, $"/connections/{index}");
            }

            if (!drivers.Add(new(connection.Target.NodeId, connection.Target.PortId)))
            {
                throw Failure(FlowCompilationDiagnosticCode.DuplicateInputDriver, $"/connections/{index}/target");
            }
        }

        foreach (var node in source.Nodes)
        {
            foreach (var input in shapes[node.Id].Values.Where(port => port.Direction == DataDirection.Input))
            {
                if (!drivers.Contains(new(node.Id, input.Id)))
                {
                    throw Failure(FlowCompilationDiagnosticCode.MissingInputDriver, $"/nodes/{Escape(node.Id)}/ports/{Escape(input.Id)}");
                }
            }
        }

        ValidatePointReferences(source.Nodes);
        ValidateAcyclic(source, nodes);
    }

    /*
     * Validate the flow's externally visible input/output interface.
     *
     * The interface is separate from the internal node graph: it defines values
     * that another flow or host can supply to FlowInput nodes or receive from
     * FlowOutput nodes. This method validates the interface-level limits and then
     * delegates validation of individual entries to ValidateInterfaceEntries().
     */
    private static void ValidateInterface(ExecutableFlowSource source)
    {
        if (source.Interface.SchemaVersion != 1)
        {
            throw Failure(FlowCompilationDiagnosticCode.UnsupportedInterfaceSchema, "/interface/schemaVersion", 1);
        }

        if (source.Interface.Inputs.Count > 64 || source.Interface.Outputs.Count > 64)
        {
            throw Failure(FlowCompilationDiagnosticCode.InterfaceLimitExceeded, "/interface", 64);
        }

        ValidateInterfaceEntries(source.Interface.Inputs.Select(entry => new InterfaceRecord(entry.Id, entry.Name, entry.DataType, entry.Units, entry.DefaultValue)), "/interface/inputs");
        ValidateInterfaceEntries(source.Interface.Outputs.Select(entry => new InterfaceRecord(entry.Id, entry.Name, entry.DataType, entry.Units, null)), "/interface/outputs");
    }

    /*
     * Validate one interface collection (either inputs or outputs).
     *
     * IDs are unique using exact ordinal comparison because they are machine
     * identifiers. Names are unique case-insensitively because they are
     * human-facing labels. Numeric entries may declare engineering units;
     * Boolean entries may not. If a default value is present, its JSON type must
     * match the declared interface data type.
     */
    private static void ValidateInterfaceEntries(IEnumerable<InterfaceRecord> entries, string path)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (entry, index) in entries.Select((entry, index) => (entry, index)))
        {
            ValidateIdentifier(entry.Id, $"{path}/{index}/id", 63);
            if (string.IsNullOrWhiteSpace(entry.Name) || Encoding.UTF8.GetByteCount(entry.Name) > 255 || !ids.Add(entry.Id) || !names.Add(entry.Name))
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidInterfaceEntry, $"{path}/{index}");
            }

            if (entry.DataType is not (DataType.Boolean or DataType.Number))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnsupportedInterfaceType, $"{path}/{index}/dataType");
            }

            if (entry.DataType != DataType.Number && !string.IsNullOrEmpty(entry.Units))
            {
                throw Failure(FlowCompilationDiagnosticCode.IncompatibleInterfaceUnits, $"{path}/{index}/units");
            }

            if (entry.DefaultValue is { } value && !DefaultMatches(value, entry.DataType))
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidInterfaceDefault, $"{path}/{index}/defaultValue");
            }
        }
    }

    /*
     * Check that an interface default value can represent the declared data type.
     * Number defaults must also be finite because NaN and infinity are not valid
     * executable numeric defaults for this profile.
     */
    private static bool DefaultMatches(JsonElement value, DataType type)
    {
        return type switch
        {
            DataType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            DataType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number),
            _ => false
        };
    }

    /*
     * Resolve the interface declaration referenced by a FlowInput or FlowOutput
     * node and normalize it into the compiler's internal InterfaceRecord shape.
     *
     * FlowInput searches source.Interface.Inputs and preserves its optional
     * default value. FlowOutput searches source.Interface.Outputs and has no
     * default value. A missing reference is reported against the node's
     * configuration rather than allowed to fail later during encoding.
     */
    private static InterfaceRecord InterfaceEntry(ExecutableFlowSource source, ExecutableFlowNode node)
    {
        var id = node.Configuration["interfaceId"].GetString();
        var entry = node.Kind == FlowNodeKind.FlowInput
            ? source.Interface.Inputs.Where(item => item.Id == id).Select(item => new InterfaceRecord(item.Id, item.Name, item.DataType, item.Units, item.DefaultValue)).SingleOrDefault()
            : source.Interface.Outputs.Where(item => item.Id == id).Select(item => new InterfaceRecord(item.Id, item.Name, item.DataType, item.Units, null)).SingleOrDefault();
        return entry ?? throw Failure(FlowCompilationDiagnosticCode.MissingInterfaceReference, $"/nodes/{Escape(node.Id)}/configuration/interfaceId");
    }

    /*
     * Return the executable VM data type for the interface entry referenced by
     * this FlowInput/FlowOutput node. The explicit switch also prevents an
     * unsupported future interface type from silently entering Flow IL v1.
     */
    private static DataType InterfaceDataType(ExecutableFlowSource source, ExecutableFlowNode node)
    {
        return InterfaceEntry(source, node).DataType switch
        {
            DataType.Boolean => DataType.Boolean,
            DataType.Number => DataType.Number,
            _ => throw new UnreachableException()
        };
    }

    /*
     * Return the engineering units declared by the interface entry referenced by
     * this FlowInput/FlowOutput node. Null means the value is dimensionless or no
     * units were declared.
     */
    private static string? InterfaceUnits(ExecutableFlowSource source, ExecutableFlowNode node)
    {
        return InterfaceEntry(source, node).Units;
    }

    /*
     * Validate the configuration object for one node according to its node kind.
     *
     * Graph validation proves that ports and connections are structurally valid;
     * this method proves that node-specific settings are usable. Examples include
     * required point/interface IDs, finite numeric constants, comparator operators,
     * timer durations, and enabled flags. Keeping these checks here means later
     * instruction generation can read configuration values without repeatedly
     * defending against malformed source data.
     */
    private static void ValidateConfiguration(ExecutableFlowSource source, ExecutableFlowNode node, int index)
    {
        var path = $"/nodes/{index}/configuration";
        if (node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput)
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("interfaceId", out var reference)
                || reference.ValueKind != JsonValueKind.String
                || reference.GetString() is not string interfaceId)
            {
                throw Failure(FlowCompilationDiagnosticCode.MissingInterfaceId, $"{path}/interfaceId");
            }

            ValidateIdentifier(interfaceId, $"{path}/interfaceId", 63);
            _ = InterfaceEntry(source, node);
        }
        else if (node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput)
        {
            if (!node.Configuration.TryGetValue("pointId", out var point)
                || point.ValueKind != JsonValueKind.String
                || point.GetString() is not string pointId)
            {
                throw Failure(FlowCompilationDiagnosticCode.MissingPointId, path);
            }

            if (node.Configuration.Keys.Any(key => key is not ("pointId" or "units")))
            {
                throw Failure(FlowCompilationDiagnosticCode.UnexpectedPointConfigurationProperty, path);
            }

            if (node.Configuration.TryGetValue("units", out var units) &&
                units.ValueKind != JsonValueKind.String)
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidPointUnits, $"{path}/units");
            }

            const int MaxIdentifierBytes = 63;
            ValidateIdentifier(pointId, $"{path}/pointId", MaxIdentifierBytes);
        }
        else if (node.Kind == FlowNodeKind.DigitalConstant)
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("value", out var value)
                || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidBooleanConfiguration, path);
            }
        }
        else if (node.Kind is FlowNodeKind.NumericConstant or FlowNodeKind.Memory)
        {
            ValidateFiniteNumber(node, path, "value");
        }
        else if (node.Kind == FlowNodeKind.Comparator)
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("operator", out var comparison)
                || comparison.ValueKind != JsonValueKind.String
                || comparison.GetString() is not ("lt" or "lte" or "eq" or "gte" or "gt" or "ne"))
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidComparisonOperator, path);
            }
        }
        else if (node.Kind is FlowNodeKind.LevelShifter or FlowNodeKind.Line)
        {
            if (node.Configuration.Count != 2)
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidGainOffsetConfiguration, path);
            }

            ValidateFiniteNumber(node, path, "gain");
            ValidateFiniteNumber(node, path, "offset");
        }
        else if (node.Kind is FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer)
        {
            ValidateFiniteNumber(node, path, "durationMs");
            var duration = node.Configuration["durationMs"].GetDouble();
            if (node.Configuration.Count != 1 || duration < 0D || duration > uint.MaxValue)
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidTimerDuration, path, 0, uint.MaxValue);
            }
        }
        else if (node.Kind == FlowNodeKind.Clamp)
        {
            if (node.Configuration.Count != 2)
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidClampConfiguration, path);
            }

            ValidateFiniteNumber(node, path, "minimum");
            ValidateFiniteNumber(node, path, "maximum");
            if (node.Configuration["minimum"].GetDouble() > node.Configuration["maximum"].GetDouble())
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidClampRange, path);
            }
        }
        else if (node.Kind is FlowNodeKind.Schedule or FlowNodeKind.Calendar)
        {
            if (node.Configuration.Count != 1 || !node.Configuration.TryGetValue("enabled", out var enabled) ||
                enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Failure(FlowCompilationDiagnosticCode.InvalidEnabledConfiguration, path);
            }
        }
        else if (node.Configuration.Count != 0)
        {
            throw Failure(FlowCompilationDiagnosticCode.UnexpectedNodeConfiguration, path);
        }
    }

    /*
     * Require one named configuration property to contain a finite JSON number.
     * Finite means an ordinary numeric value: NaN and positive/negative infinity
     * are rejected because they would make execution and canonical encoding less
     * predictable.
     */
    private static void ValidateFiniteNumber(ExecutableFlowNode node, string path, string key)
    {
        if (!node.Configuration.TryGetValue(key, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var number)
            || !double.IsFinite(number))
        {
            throw Failure(FlowCompilationDiagnosticCode.InvalidFiniteNumber, $"{path}/{key}");
        }
    }

    /*
     * Enumerate every literal constant required to encode one source node.
     *
     * The compiler first gathers these values from all nodes, removes duplicates,
     * and sorts them into the canonical constant pool. Instructions then refer to
     * constants by pool index instead of embedding literal values directly.
     *
     * A node may contribute zero, one, or several constants depending on its kind;
     * for example Clamp contributes minimum and maximum while Add contributes none.
     */
    private static IEnumerable<ConstantRecord> ConstantsFor(ExecutableFlowNode node)
    {
        if (node.Kind == FlowNodeKind.DigitalConstant)
        {
            yield return GetBooleanConstant(node.Configuration["value"].GetBoolean());
        }
        else if (node.Kind is FlowNodeKind.NumericConstant or FlowNodeKind.Memory)
        {
            yield return GetNumericConstant(node, "value");
        }
        else if (node.Kind is FlowNodeKind.LevelShifter or FlowNodeKind.Line)
        {
            yield return GetNumericConstant(node, "gain");
            yield return GetNumericConstant(node, "offset");
        }
        else if (node.Kind == FlowNodeKind.Clamp)
        {
            yield return GetNumericConstant(node, "minimum");
            yield return GetNumericConstant(node, "maximum");
        }
        else if (node.Kind is FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer)
        {
            yield return GetNumericConstant(node, "durationMs");
        }
        else if (node.Kind is FlowNodeKind.RisingEdge or FlowNodeKind.Pulse)
        {
            yield return GetBooleanConstant(false);
        }
        else if (node.Kind is FlowNodeKind.Schedule or FlowNodeKind.Calendar)
        {
            yield return GetBooleanConstant(node.Configuration["enabled"].GetBoolean());
        }
    }

    /*
     * Normalize a Boolean literal into the common ConstantRecord representation.
     * Boolean false is stored as numeric 0 and true as numeric 1; DataType keeps
     * that representation distinct from an actual numeric constant.
     */
    private static ConstantRecord GetBooleanConstant(bool value)
    {
        return new(DataType.Boolean, value ? 1D : 0D);
    }

    /*
     * Read one numeric configuration property and wrap it in the same internal
     * ConstantRecord representation used to build the canonical constant pool.
     */
    private static ConstantRecord GetNumericConstant(ExecutableFlowNode node, string key)
    {
        return new(DataType.Number, node.Configuration[key].GetDouble());
    }

    /*
     * Translate a constant value into its zero-based index in the canonical
     * constant pool. Instructions encode this 16-bit index, not the literal
     * constant itself. ConstantsFor() populated the pool before this lookup occurs.
     */
    private static ushort ConstantIndex(ConstantRecord[] constants, ConstantRecord value)
    {
        return checked((ushort)Array.IndexOf(constants, value));
    }

    /*
     * Constant byte encoding is intentionally explicit and canonical.
     * Boolean record: 4 bytes total.
     * Numeric record: 12 bytes total; the IEEE-754 binary64 bit pattern is written
     * little-endian by F64().
     */
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

    /*
     * Determine the data type stored in a node's transient result slot.
     *
     * FlowInput/FlowOutput are resolved from their interface declaration. Known
     * numeric node kinds produce Number; the remaining supported executable node
     * kinds produce Boolean. This value is written into the slot table and is also
     * used when resolving output point bindings.
     */
    private static DataType ResultDataType(ExecutableFlowSource source, ExecutableFlowNode node)
    {
        if (node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput)
        {
            return InterfaceDataType(source, node);
        }

        if (node.Kind is
            FlowNodeKind.NumericConstant or
            FlowNodeKind.Add or
            FlowNodeKind.LevelShifter or
            FlowNodeKind.AnalogInput or
            FlowNodeKind.AnalogOutput or
            FlowNodeKind.Memory or
            FlowNodeKind.Average or
            FlowNodeKind.Calculator or
            FlowNodeKind.Clamp or
            FlowNodeKind.Min or
            FlowNodeKind.Max or
            FlowNodeKind.Line or
            FlowNodeKind.Selector)
        {
            return DataType.Number;
        }

        return DataType.Boolean;
    }

    /*
     * Convert the comparator's authoring string into the compact numeric operation
     * code stored in the instruction Auxiliary field. Validation has already
     * restricted the string to this supported set, so any other value indicates an
     * internal compiler inconsistency.
     */
    private static ushort ComparatorCode(ExecutableFlowNode node)
    {
        return node.Configuration["operator"].GetString() switch
        {
            "lt" => 1,
            "lte" => 2,
            "eq" => 3,
            "gte" => 4,
            "gt" => 5,
            "ne" => 6,
            _ => throw new UnreachableException()
        };
    }

    /*
     * Resolve one connection endpoint to the FlowPort definition that describes it.
     *
     * An endpoint is valid only when both its node ID and port ID exist in the
     * effective per-node shape table. Returning the FlowPort lets the caller then
     * validate direction and data-type compatibility. The connection index and
     * endpoint name are carried only so any failure can point at the exact source
     * location that is invalid.
     */
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
            throw Failure(FlowCompilationDiagnosticCode.EndpointNotFound, $"/connections/{connectionIndex}/{endpointName}");
        }

        return port;
    }

    /*
     * Ensure a physical output point has at most one node proposing its value.
     *
     * Multiple input nodes may read the same physical point, but two DigitalOutput
     * or AnalogOutput nodes targeting the same point would create competing output
     * drivers. The HashSet records each output point ID as it is encountered and
     * rejects the second occurrence.
     */
    private static void ValidatePointReferences(IReadOnlyList<ExecutableFlowNode> nodes)
    {
        var outputPoints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes.Where(node => node.Kind is FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogOutput))
        {
            var pointId = node.Configuration["pointId"].GetString()!;
            if (!outputPoints.Add(pointId))
            {
                throw Failure(FlowCompilationDiagnosticCode.DuplicatePointOutputDriver, $"/points/{Escape(pointId)}", pointId);
            }
        }
    }

    /*
     * Verify that the same-scan dependency graph has a valid execution order.
     *
     * This is a Kahn topological-sort check. Incoming connections to Memory nodes
     * are intentionally ignored because Memory reads previously committed state;
     * its new input is committed after the main scan. Memory can therefore break a
     * feedback path that would otherwise be a combinational cycle.
     *
     * If every node can be removed from the graph, the graph is schedulable. If
     * nodes remain with non-zero indegree, those nodes participate in or depend on
     * a same-scan cycle and compilation fails.
     */
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
            if (nodes[connection.Target.NodeId].Kind == FlowNodeKind.Memory)
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
            throw Failure(FlowCompilationDiagnosticCode.CombinationalCycle, $"/nodes/{Escape(first)}", first);
        }
    }

    /*
     * Build the canonical table of external point/interface bindings referenced by
     * executable I/O nodes.
     *
     * Each record captures the binding ID, direction, data type, units, and whether
     * it represents a flow-interface endpoint. Duplicate equivalent records are
     * removed, then the table is sorted so equivalent source produces stable point
     * indices and therefore deterministic instruction bytes.
     *
     * The resulting array is later encoded as section 2 and is also used by
     * PointIndex() when instructions need to refer to a binding.
     */
    private static PointRecord[] BuildPoints(
        ExecutableFlowSource source,
        IReadOnlyList<ExecutableFlowNode> nodes,
        IReadOnlyList<FlowPoint> resolvedPoints) =>
    [
        .. nodes
            .Where(node => node.Kind is
                FlowNodeKind.DigitalInput or
                FlowNodeKind.DigitalOutput or
                FlowNodeKind.AnalogInput or
                FlowNodeKind.AnalogOutput or
                FlowNodeKind.FlowInput or
                FlowNodeKind.FlowOutput)
            .Select(node => new PointRecord(
                // Id
                node.Configuration[
                    node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput
                        ? "interfaceId"
                        : "pointId"]
                    .GetString()!,

                // Direction
                node.Kind switch
                {
                    FlowNodeKind.DigitalInput => DataDirection.Input,
                    FlowNodeKind.AnalogInput => DataDirection.Input,
                    FlowNodeKind.FlowInput => DataDirection.Input,

                    FlowNodeKind.DigitalOutput => DataDirection.Output,
                    FlowNodeKind.AnalogOutput => DataDirection.Output,
                    FlowNodeKind.FlowOutput => DataDirection.Output,
                },

                // Type
                node.Kind switch
                {
                    FlowNodeKind.DigitalInput or
                    FlowNodeKind.DigitalOutput
                        => DataType.Boolean,

                    FlowNodeKind.AnalogInput or
                    FlowNodeKind.AnalogOutput
                        => DataType.Number,

                    FlowNodeKind.FlowInput or
                    FlowNodeKind.FlowOutput
                        => InterfaceDataType(source, node),

                    _ => DataType.Any
                },

                // Units
                node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput
                    ? InterfaceUnits(source, node)
                    : PointUnits(node, resolvedPoints),

                node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput
                    ? PointBindingKind.FlowInterface
                    : PointBindingKind.ControllerPoint,

                // Kind
                node.Kind))
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
        IReadOnlyList<FlowPoint> resolvedPoints)
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

    // Encode a required identifier/string as [length:u8][UTF-8 bytes].
    // The length is BYTE length, not C# char count; this matters for non-ASCII text.
    /*
     * Encode a required string in the Flow IL string8 representation:
     *
     *     +-------------+-----------------------------+
     *     | length:u8   | UTF-8 bytes                 |
     *     +-------------+-----------------------------+
     *
     * The length is the number of encoded UTF-8 BYTES, not the number of C# chars.
     * checked(byte) makes values longer than 255 encoded bytes fail rather than
     * truncating the length field.
     */
    private static byte[] String8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat([checked((byte)bytes.Length)], bytes);
    }

    // Same physical string8 representation, but permits a zero length byte.
    /*
     * Encode the same string8 representation as String8(), while documenting that
     * an empty string is meaningful at this call site. An empty value becomes one
     * zero length byte and no following payload bytes.
     */
    private static byte[] String8AllowEmpty(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat([checked((byte)bytes.Length)], bytes);
    }

    // Materialize a u16 as two little-endian bytes. Returning byte[] makes field
    // composition via Concat visibly match the binary record diagrams above.
    /*
     * Materialize one unsigned 16-bit integer as exactly two little-endian bytes.
     * Returning byte[] allows binary records to be assembled field-by-field with
     * Concat() in the same order shown by the format diagrams. checked() prevents
     * negative or oversized integers from silently wrapping.
     */
    private static byte[] U16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
        return bytes;
    }

    // Materialize a u32 as four little-endian bytes.
    /*
     * Materialize one unsigned 32-bit integer as exactly four little-endian bytes
     * for inclusion in an encoded Flow IL field.
     */
    private static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    /*
     * Propagate engineering units through the scheduled numeric graph and reject
     * operations whose units are incompatible.
     *
     * 'units' maps node ID -> units of that node's result. Processing in schedule
     * order means the units of every upstream source are already known when a node
     * is examined. Operations such as Add, Comparator, Min, and Max require their
     * two numeric inputs to have identical units; pass-through numeric operations
     * preserve the units of their input.
     *
     * Physical and flow outputs are checked against the units declared by their
     * destination binding so a value cannot be written to an incompatible endpoint.
     */
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
                FlowNodeKind.AnalogInput => request.Target.Points.SingleOrDefault(point => point.Id == node.Configuration["pointId"].GetString())?.Units,
                FlowNodeKind.FlowInput => InterfaceUnits(source, node),
                FlowNodeKind.NumericConstant => null,
                FlowNodeKind.Add => RequireMatchingUnits(source, units, id, "a", "b"),
                FlowNodeKind.Comparator => RequireMatchingUnits(source, units, id, "a", "b"),
                FlowNodeKind.LevelShifter => units[SourceNode(source, id, "in")],
                FlowNodeKind.Average or FlowNodeKind.Calculator or FlowNodeKind.Clamp or FlowNodeKind.Line => units[SourceNode(source, id, "input")],
                FlowNodeKind.Min or FlowNodeKind.Max or FlowNodeKind.Selector => RequireMatchingUnits(source, units, id, "a", "b"),
                _ => null
            };

            units[id] = value;

            if (node.Kind == FlowNodeKind.AnalogOutput)
            {
                var inputUnits = units[SourceNode(source, id, "in")];

                var pointUnits = request.Target.Points.SingleOrDefault(point => point.Id == node.Configuration["pointId"].GetString())?.Units;

                if (!string.Equals(inputUnits, pointUnits, StringComparison.Ordinal))
                {
                    throw Failure(FlowCompilationDiagnosticCode.AnalogOutputUnitMismatch, $"/nodes/{Escape(id)}");
                }
            }

            if (node.Kind == FlowNodeKind.FlowOutput)
            {
                var inputUnits = units[SourceNode(source, id, "value")];
                if (!string.Equals(inputUnits, InterfaceUnits(source, node), StringComparison.Ordinal))
                {
                    throw Failure(FlowCompilationDiagnosticCode.FlowOutputUnitMismatch, $"/nodes/{Escape(id)}/ports/value");
                }
            }
        }
    }

    /*
     * Resolve the units arriving at two input ports and require them to match.
     *
     * The returned value is the common unit and can therefore become the result
     * unit for operations such as Add, Min, and Max. Comparator also uses this
     * check even though its own result is Boolean, because comparing unlike units
     * would be semantically invalid.
     */
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
            throw Failure(FlowCompilationDiagnosticCode.NumericOperandUnitMismatch, $"/nodes/{Escape(nodeId)}");
        }

        return left;
    }

    /*
     * Return the source-node ID connected to one target input port. Validation has
     * already guaranteed exactly one driver, so Single() expresses that invariant.
     * This helper is used when later passes need information about the upstream node
     * rather than merely its slot index.
     */
    private static string SourceNode(ExecutableFlowSource source, string nodeId, string portId)
    {
        return source.Connections.Single(connection =>
            connection.Target.NodeId == nodeId && connection.Target.PortId == portId).Source.NodeId;
    }

    // Preserve the exact IEEE-754 binary64 bit pattern and then emit those 64 bits
    // little-endian. This avoids culture/text formatting and round-trip ambiguity.
    private static byte[] F64(double value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
        return bytes;
    }

    /*
     * Combine multiple byte arrays into one continuous byte array.
     *
     * The bytes from each input array are copied into the result in the same
     * order that the arrays are supplied.
     *
     * For example:
     *
     *     part 1          part 2          part 3
     *     +----------+    +-------+       +-------------+
     *     | AA BB CC |    | 01 02 |       | FF EE DD CC |
     *     +----------+    +-------+       +-------------+
     *           \             |                 /
     *            \            |                /
     *             +-----------+---------------+
     *                         |
     *                         v
     *
     *     result
     *     +----------------------------------+
     *     | AA BB CC 01 02 FF EE DD CC       |
     *     +----------------------------------+
     *
     * This method only joins the supplied bytes. It does not add any additional
     * information between the parts.
     *
     * If the binary format requires information such as a length, identifier,
     * header, or other structure, that information must be supplied as one of
     * the input byte arrays before calling Concat().
     */
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

    // In-place fixed-offset envelope writer; offset is a byte offset.
    /*
     * Write a 16-bit unsigned value directly into an existing byte array at an
     * exact byte offset. This is used for fixed-position envelope fields where the
     * destination buffer already exists.
     */
    private static void WriteU16(byte[] target, int offset, int value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset), checked((ushort)value));
    }

    // In-place fixed-offset envelope writer for four-byte little-endian fields.
    /*
     * Write a 32-bit unsigned value directly into an existing byte array at an
     * exact byte offset using little-endian byte order.
     */
    private static void WriteU32(byte[] target, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);
    }

    /*
     * Envelope flow-ID field writer. At 'offset' write one length byte, then the
     * UTF-8 bytes. The remainder of the fixed envelope field is already zero because
     * the envelope byte[] was zero-initialized.
     */
    private static void WritePaddedIdentifier(byte[] target, int offset, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        target[offset] = checked((byte)bytes.Length);
        bytes.CopyTo(target, offset + 1);
    }

    /*
     * Validate an identifier used by the executable format. It must match the
     * restricted identifier syntax and its UTF-8 encoded form must fit within the
     * field-specific byte limit. The diagnostic path identifies the exact source
     * property that supplied the invalid value.
     */
    private static void ValidateIdentifier(string value, string path, int maximumBytes)
    {
        if (!IdentifierRegex().IsMatch(value) || Encoding.UTF8.GetByteCount(value) > maximumBytes)
        {
            throw Failure(FlowCompilationDiagnosticCode.InvalidIdentifier, path);
        }
    }

    /*
     * Construct the compiler's standard exception containing one structured
     * diagnostic. Centralizing this keeps validation failures consistent: every
     * diagnostic carries a stable machine-readable code, a source path, and a
     * human-readable explanation.
     */
    private static FlowCompilationException Failure(
        FlowCompilationDiagnosticCode code,
        string path,
        params object?[] arguments)
    {
        return new([FlowCompilationDiagnostics.Create(code, path, arguments)]);
    }

    /*
     * Escape a value so it can be safely inserted as one segment of a
     * JSON-Pointer-style path used in compiler error locations.
     *
     * Compiler errors use paths such as:
     *
     *     /nodes/3/id
     *     /points/temperature/revision
     *
     * In these paths, '/' separates path segments and '~' introduces an escape
     * sequence. If an identifier itself contains either character, using it
     * directly would change the meaning of the path.
     *
     * JSON Pointer escaping represents these characters as:
     *
     *     +-----------+---------+
     *     | Character | Encoded |
     *     +-----------+---------+
     *     |     ~     |   ~0    |
     *     |     /     |   ~1    |
     *     +-----------+---------+
     *
     * For example:
     *
     *     value:
     *
     *         floor/1~temperature
     *
     *     escaped:
     *
     *         floor~11~0temperature
     *
     *     path:
     *
     *         /points/floor~11~0temperature/revision
     *
     * The '~' replacement must happen first. If '/' were replaced first, the
     * newly introduced '~' in "~1" would itself be escaped and incorrectly
     * become "~01".
     */
    private static string Escape(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
    }

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
    private static string AuthoringLabel(ExecutableFlowNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Label))
        {
            return node.Label;
        }

        var value = CaptialCaseBoundaryRegex().Replace(node.Kind.ToString(), " $1").Trim();

        return value.Length > 0
            ? char.ToUpperInvariant(value[0]) + value[1..]
            : node.Kind.ToString();
    }

    [GeneratedRegex("([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex CaptialCaseBoundaryRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    /*
     * Describes how an emitted VM instruction relates back to its source designer
     * node when symbol metadata is generated.
     */
    private enum NodeInstructionRole : byte
    {
        /// <summary>
        /// The primary VM instruction generated for a source node.
        /// </summary>
        Primary = 0,

        /// <summary>
        /// An additional VM instruction generated from the same source node.
        /// </summary>
        Secondary = 1,

        /// <summary>
        /// The instruction is not associated with a source-node instruction role.
        /// </summary>
        None = byte.MaxValue
    }

    /* Describes the complete set of connection ports exposed by one node kind. */
    private sealed record FlowNodeShape(IReadOnlyList<FlowPort> Ports);

    /* Describes one named node connection point: its ID, direction, and value type. */
    private sealed record FlowPort(string Id, DataDirection Direction, DataType DataType);

    /* Uniquely identifies one input/output port on one source node. */
    private sealed record FlowPortKey(string NodeId, string PortId);

    /* Canonical compiler representation of one physical-point or flow-interface binding. */
    private sealed record PointRecord(string Id, DataDirection Direction, DataType DataType, string? Units, PointBindingKind BindingKind, FlowNodeKind Kind);

    /* Normalized representation shared by interface-input and interface-output validation. */
    private sealed record InterfaceRecord(string Id, string Name, DataType DataType, string? Units, JsonElement? DefaultValue);

    /* Typed literal stored in the canonical constant pool before binary encoding. */
    private sealed record ConstantRecord(DataType DataType, double Number);

    /*
     * Values shared while converting one scheduled source node into its primary VM
     * instruction. This removes a long repeated parameter list from the node-kind
     * specific instruction helpers without introducing mutable compiler state.
     */
    private sealed record InstructionCreationContext(
        ExecutableFlowSource Source,
        Dictionary<string, ushort> Slots,
        Dictionary<string, ushort> StateSlots,
        IReadOnlyList<PointRecord> Points,
        ConstantRecord[] Constants,
        string NodeId,
        ushort ResultSlotIndex);

    /*
     * Prepared deterministic compiler state shared by the section encoders. This
     * keeps CompileFlowIlV1() focused on orchestration without passing ten related
     * collections separately through every encoding helper.
     */
    private sealed record CompilationModel(
        ExecutableFlowSource Source,
        List<string> Schedule,
        Dictionary<string, ExecutableFlowNode> Nodes,
        Dictionary<string, ushort> Slots,
        string[] MemoryIds,
        string[] StateIds,
        Dictionary<string, ushort> StateSlots,
        PointRecord[] Points,
        ConstantRecord[] Constants,
        List<CompiledInstructionV1> Instructions);

    /*
     * Logical, not-yet-serialized representation of one Flow IL v1 instruction.
     * Slot/index fields become u16 values in section 4; NodeId and Role are compiler
     * metadata used when producing symbol/debug information.
     */
    private sealed record CompiledInstructionV1(
        Instruction Instruction,
        string NodeId,
        NodeInstructionRole Role);

    /*
     * One completely encoded section payload plus the metadata needed to create its
     * 48-byte directory entry. Bytes contains payload only, not the directory record.
     */
    private sealed record V1Section(ushort Id, uint Count, byte[] Bytes, ushort Version = 1);
}