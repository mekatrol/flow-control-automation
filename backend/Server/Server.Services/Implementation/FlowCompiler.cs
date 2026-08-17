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

    private enum NodeInstructionRole : byte
    {
        /// <summary>
        /// the node's normal/main VM instruction
        /// </summary>
        Primary = 0,

        /// <summary>
        /// an additional VM instruction generated from the same node
        /// </summary>
        Secondary = 1,

        /// <summary>
        /// The instruction has no purpose or role, it is simply a placeholder value
        /// </summary>
        None = byte.MaxValue
    }

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
            || source.Execution.InputQualityPolicy is not ("requireGood" or "propagate"))
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
        // The first 128 bytes are a fixed-position header. Fields are written later
        // with WriteU16/WriteU32 or direct byte assignment at contract-defined offsets.
        const int envelopeLength = 128;

        // Every section-directory record is:
        //   0..1   section id        u16 LE
        //   2..3   section version   u16 LE
        //   4..7   payload offset    u32 LE, absolute from artifact byte 0
        //   8..11  payload length    u32 LE
        //   12..15 record count      u32 LE
        //   16..47 SHA-256           32 raw digest bytes
        const int directoryEntryLength = 48;

        // Shorthand source variable
        var source = request.Source;

        // Get graph schedule
        var schedule = GetSchedule(source);

        // Build a dictionary of node ID -> node for fast lookup during instruction encoding.
        var nodes = source.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

        // Transient slot allocation is positional: scheduled node 0 writes slot 0,
        // scheduled node 1 writes slot 1, and so on. Because the schedule is
        // deterministic, these slot numbers are deterministic too. The ushort cast
        // also documents that operands/results are encoded as 16-bit slot indices.
        var slots = schedule.Select((id, index) => new { id, index = checked((ushort)index) })
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);

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
        var memoryIds = schedule
            .Where(id => nodes[id].Kind == FlowNodeKind.Memory)
            .ToArray();

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
        var stateIds = schedule
            .Where(id => nodes[id].Kind is
                FlowNodeKind.Memory or
                FlowNodeKind.OnDelay or
                FlowNodeKind.RisingEdge or
                FlowNodeKind.Delay or
                FlowNodeKind.Timer or
                FlowNodeKind.Pulse)
            .ToArray();

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
        var stateSlots = stateIds
            .Select((id, index) => new
            {
                id,
                index = checked((ushort)(schedule.Count + index))
            })
            .ToDictionary(
                item => item.id,
                item => item.index,
                StringComparer.Ordinal);

        var points = BuildPoints(source, [.. schedule.Select(id => nodes[id])], request.Target.Points);

        // Build a canonical constant pool before encoding instructions. Instructions
        // never embed a double directly; they carry a u16 index into this pool.
        // Sorting means equivalent resolved source produces stable pool indices and
        // therefore stable instruction bytes.
        var constants = source.Nodes.SelectMany(ConstantsFor)
            .Distinct()
            .OrderBy(constant => constant.DataType)
            .ThenBy(constant => constant.Number)
            .ToArray();

        // V1Instruction is still a logical C# record at this stage. Each item becomes
        // exactly 12 bytes only when EncodeV1Instruction() is called below.
        var instructions = new List<V1Instruction>();

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
        foreach (var id in memoryIds)
        {
            instructions.Add(new V1Instruction(
                FlowOpcode.MemoryCommit,
                ushort.MaxValue,
                InputSlot(source, slots, id, "in"),
                ushort.MaxValue,
                stateSlots[id],
                id,
                NodeInstructionRole.Secondary));
        }

        // The stream ends with one anonymous Commit. It has no result, operands,
        // auxiliary index, or source node. In bytes, all four u16 index fields are
        // therefore FF FF; the final reserved u16 is emitted separately as zero by
        // EncodeV1Instruction().
        instructions.Add(new V1Instruction(
            FlowOpcode.Commit,
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            string.Empty,
            NodeInstructionRole.None));

        // ---------------------------------------------------------------------
        // SECTION 1 — typed constants
        // ---------------------------------------------------------------------
        // EncodeConstant emits:
        //   Boolean: [type:u8][value/flags:u8][reserved:u16]          = 4 bytes
        //   Number : [type:u8][flags=0:u8][reserved:u16][f64 LE]     = 12 bytes
        // Variable record size is safe because the type prefix tells the decoder
        // whether another eight bytes follow.
        var constantSection = Concat([.. constants.Select(EncodeConstant)]);

        // ---------------------------------------------------------------------
        // SECTION 2 — point/interface bindings
        // ---------------------------------------------------------------------
        // Each record starts with four fixed bytes:
        //   direction:u8, dataType:u8, qualityPolicy:u8, bindingKind:u8
        // followed by two string8 values: binding id and units.
        // Because string8 is variable length, section 2 is parsed record-by-record,
        // not by multiplying count by a fixed record width.
        var pointSection = Concat([.. points.Select(point => Concat(
            [(byte)point.Direction, (byte)point.DataType, 1, (byte)point.Kind],
            String8(point.Id),
            String8AllowEmpty(point.Units ?? string.Empty)))]);

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
        var slotRecords = schedule.Select((id, index) => Concat(
            [2, (byte)ResultDataType(source, nodes[id])], U16(0), U16(index), U16(ushort.MaxValue))).ToList();

        slotRecords.AddRange(stateIds.Select(id => nodes[id].Kind switch
        {
            FlowNodeKind.Memory => Concat([3, 1], U16(0), U16(stateSlots[id]),
                U16(ConstantIndex(constants, GetNumericConstant(nodes[id], "value")))),

            FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer => Concat([4, 1], U16(0), U16(stateSlots[id]),
                U16(ConstantIndex(constants, GetNumericConstant(nodes[id], "durationMs")))),

            FlowNodeKind.RisingEdge or FlowNodeKind.Pulse => Concat([5, 1], U16(0), U16(stateSlots[id]),
                U16(ConstantIndex(constants, GetBooleanConstant(false)))),
            _ => throw new UnreachableException()
        }));

        // Flatten all 8-byte records into the section payload. Concat inserts no
        // delimiter or padding between records.
        var slotSection = Concat([.. slotRecords]);

        // ---------------------------------------------------------------------
        // SECTION 4 — scheduled instructions
        // ---------------------------------------------------------------------
        // Fixed width: instructions.Count * 12 bytes exactly.
        var instructionSection = Concat([.. instructions.Select(EncodeV1Instruction)]);

        // ---------------------------------------------------------------------
        // SECTION 5 — commit plan
        // ---------------------------------------------------------------------
        // Each record is exactly 8 bytes:
        //   kind:u8, flags:u8, target:u16, sourceSlot:u16, policy:u16
        // It describes what becomes externally/committed-state visible at the scan
        // boundary, rather than changing state/output during instruction execution.
        var commitRecords = memoryIds.Select(id => Concat([1, 0], U16(stateSlots[id]), U16(InputSlot(source, slots, id, "in")), U16(0))).ToList();

        commitRecords.AddRange(schedule.Where(id => nodes[id].Kind is FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogOutput or FlowNodeKind.FlowOutput).Select(id => Concat(
            [2, 0],
            U16(PointIndex(points, nodes[id], DataDirection.Output, ResultDataType(source, nodes[id]))),
            U16(slots[id]),
            U16(0))));

        commitRecords.AddRange(stateIds.Where(id => nodes[id].Kind is FlowNodeKind.OnDelay or FlowNodeKind.RisingEdge or FlowNodeKind.Delay or FlowNodeKind.Timer or FlowNodeKind.Pulse).Select(id => Concat(
            [1, 0], U16(stateSlots[id]), U16(slots[id]), U16(0))));

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
        var symbolSection = Concat([.. instructions.Select((instruction, index) =>
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
        var debugSection = Concat([.. instructions.Where(instruction => instruction.NodeId.Length > 0).Select((instruction, index) => Concat(U16(index), U16(instruction.ResultSlotIndex), String8(instruction.NodeId)))]);
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

        // Package the eight already-encoded payloads with their IDs and logical
        // record counts. V1Section itself is not serialized directly; the loop below
        // turns this metadata into directory bytes.
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

        // First section payload begins immediately after:
        //   128-byte envelope + 8 * 48-byte directory = byte offset 512.
        // 'offset' always means an absolute byte position in the final artifact.
        var offset = checked((uint)(envelopeLength + (sections.Length * directoryEntryLength)));
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

        if (source.Nodes.Any(node => node.Kind is FlowNodeKind.Nand or FlowNodeKind.Nor or FlowNodeKind.Xor or FlowNodeKind.Xnor))
        {
            capabilities |= ExpandedBooleanCapability;
        }

        if (source.Nodes.Any(node =>
            node.Kind is FlowNodeKind.NumericConstant or FlowNodeKind.Add or FlowNodeKind.Comparator or FlowNodeKind.LevelShifter or FlowNodeKind.Average or FlowNodeKind.Calculator or FlowNodeKind.Clamp or FlowNodeKind.Min or FlowNodeKind.Max or FlowNodeKind.Line or FlowNodeKind.Selector) ||
            points.Any(point => point.DataType == DataType.Number))
        {
            capabilities |= NumericCapability;
        }

        if (source.Nodes.Any(node => node.Kind == FlowNodeKind.Comparator))
        {
            capabilities |= ComparisonCapability;
        }

        if (source.Nodes.Any(node => node.Kind == FlowNodeKind.LevelShifter))
        {
            capabilities |= LevelShifterCapability;
        }

        if (source.Nodes.Any(node => node.Kind == FlowNodeKind.QualityGood))
        {
            capabilities |= QualityCapability;
        }

        if (source.Nodes.Any(node => node.Kind is FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer))
        {
            capabilities |= TimerCapability;
        }

        if (source.Nodes.Any(node => node.Kind is FlowNodeKind.RisingEdge or FlowNodeKind.Pulse))
        {
            capabilities |= EventCapability;
        }

        var workingBytes = checked((uint)((schedule.Count + stateIds.Length) * 32));

        // ---------------------------------------------------------------------
        // FIXED 128-BYTE ENVELOPE
        // ---------------------------------------------------------------------
        // New byte[] is zero-initialized, which is important: every reserved byte
        // that is not explicitly written below remains canonical zero.
        var envelope = new byte[envelopeLength];

        // bytes 0..3: ASCII magic 46 49 4C 31 ("FIL1").
        "FIL1"u8.CopyTo(envelope);
        // bytes 4..5   : IL version, u16 LE.
        WriteU16(envelope, 4, 1);
        // bytes 6..7   : envelope length = 128, u16 LE.
        WriteU16(envelope, 6, envelopeLength);
        // bytes 8..11  : exact final artifact length, u32 LE.
        WriteU32(envelope, 8, offset);
        // bytes 12..15 : flags. This implementation writes bit 0 = 1.
        WriteU32(envelope, 12, 1);
        // bytes 16..19 : flow revision.
        WriteU32(envelope, 16, source.Revision);
        // bytes 20..23 : resolved controller-template revision.
        WriteU32(envelope, 20, source.ControllerTemplateRevision);
        // bytes 24..25 : minimum host ABI.
        WriteU16(envelope, 24, 1);
        // bytes 26..27 : section count (8).
        WriteU16(envelope, 26, sections.Length);
        // byte 28      : input-quality policy. bytes 29..31 stay zero.
        envelope[28] = source.Execution.InputQualityPolicy == "requireGood" ? (byte)1 : (byte)2;
        // bytes 32..35 : bounded maximum work per scan.
        WriteU32(envelope, 32, checked((uint)instructions.Count));
        // bytes 36..43 : required-capability bitmap, u64 LE.
        BinaryPrimitives.WriteUInt64LittleEndian(envelope.AsSpan(36), capabilities);
        // bytes 44..47 : VM working-byte estimate.
        WriteU32(envelope, 44, workingBytes);
        // bytes 48..51 : maximum snapshot bytes.
        WriteU32(envelope, 48, 16384);
        // bytes 52..115: one-byte flow-ID byte length + UTF-8 ID + zero padding.
        WritePaddedIdentifier(envelope, 52, source.Id);
        // bytes 116..119: directory absolute offset (= 128).
        // bytes 120..127 remain zero from array initialization.
        WriteU32(envelope, 116, envelopeLength);

        // Final byte build. This is the only place the complete artifact is assembled:
        //   [128-byte envelope]
        //   [384-byte directory when section count is 8]
        //   [section 1 bytes][section 2 bytes]...[section 8 bytes]
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
            throw Failure(
                "cyclic_dependency",
                "/connections",
                "Flow contains a cyclic dependency.");
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
 * ushort.MaxValue (0xFFFF) represents an unused slot/index field.
 */
    private static V1Instruction CreatePrimaryInstruction(
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
        // ushort.MaxValue (0xFFFF) is used wherever an index field is unused.
        return node.Kind switch
        {
            FlowNodeKind.DigitalInput =>
                new(
                    FlowOpcode.PointInput,
                    resultSlotIndex,
                    ushort.MaxValue,
                    ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Input, DataType.Boolean),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.AnalogInput =>
                new(
                    FlowOpcode.PointInput,
                    resultSlotIndex,
                    ushort.MaxValue,
                    ushort.MaxValue,
                    PointIndex(points, node, DataDirection.Input, DataType.Number),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.FlowInput =>
                new(
                    FlowOpcode.PointInput,
                    resultSlotIndex,
                    ushort.MaxValue,
                    ushort.MaxValue,
                    PointIndex(
                        points,
                        node,
                        DataDirection.Input,
                        InterfaceDataType(source, node)),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.DigitalConstant =>
                new(
                    FlowOpcode.DigitalConstant,
                    resultSlotIndex,
                    ushort.MaxValue,
                    ushort.MaxValue,
                    ConstantIndex(
                        constants,
                        GetBooleanConstant(
                            node.Configuration["value"].GetBoolean())),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Not =>
                new(
                    FlowOpcode.Not,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "in"),
                    ushort.MaxValue,
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.And =>
                new(
                    FlowOpcode.And,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Or =>
                new(
                    FlowOpcode.Or,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Nand =>
                new(
                    FlowOpcode.Nand,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Nor =>
                new(
                    FlowOpcode.Nor,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Xor =>
                new(
                    FlowOpcode.Xor,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Xnor =>
                new(
                    FlowOpcode.Xnor,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.NumericConstant =>
                new(
                    FlowOpcode.NumericConstant,
                    resultSlotIndex,
                    ushort.MaxValue,
                    ushort.MaxValue,
                    ConstantIndex(
                        constants,
                        GetNumericConstant(node, "value")),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Add =>
                new(
                    FlowOpcode.Add,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Comparator =>
                new(
                    FlowOpcode.Comparator,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ComparatorCode(node),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.LevelShifter =>
                new(
                    FlowOpcode.LevelShifter,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "in"),
                    ConstantIndex(constants, GetNumericConstant(node, "gain")),
                    ConstantIndex(constants, GetNumericConstant(node, "offset")),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.QualityGood =>
                new(
                    FlowOpcode.QualityGood,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "in"),
                    ushort.MaxValue,
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.OnDelay =>
                new(
                    FlowOpcode.OnDelay,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "in"),
                    ushort.MaxValue,
                    stateSlots[nodeId],
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.RisingEdge =>
                new(
                    FlowOpcode.RisingEdge,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "in"),
                    ushort.MaxValue,
                    stateSlots[nodeId],
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Memory =>
                new(
                    FlowOpcode.Memory,
                    resultSlotIndex,
                    ushort.MaxValue,
                    ushort.MaxValue,
                    stateSlots[nodeId],
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.DigitalOutput =>
                new(
                    FlowOpcode.PointOutput,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "in"),
                    ushort.MaxValue,
                    PointIndex(
                        points,
                        node,
                        DataDirection.Output,
                        DataType.Boolean),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.AnalogOutput =>
                new(
                    FlowOpcode.PointOutput,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "in"),
                    ushort.MaxValue,
                    PointIndex(
                        points,
                        node,
                        DataDirection.Output,
                        DataType.Number),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.FlowOutput =>
                new(
                    FlowOpcode.PointOutput,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "value"),
                    ushort.MaxValue,
                    PointIndex(
                        points,
                        node,
                        DataDirection.Output,
                        InterfaceDataType(source, node)),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Average or
            FlowNodeKind.Calculator or
            FlowNodeKind.Split or
            FlowNodeKind.Override =>
                new(
                    FlowOpcode.Passthrough,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "input"),
                    ushort.MaxValue,
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Min =>
                new(
                    FlowOpcode.Min,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Max =>
                new(
                    FlowOpcode.Max,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Clamp =>
                new(
                    FlowOpcode.Clamp,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "input"),
                    ConstantIndex(
                        constants,
                        GetNumericConstant(node, "minimum")),
                    ConstantIndex(
                        constants,
                        GetNumericConstant(node, "maximum")),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Line =>
                new(
                    FlowOpcode.Line,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "input"),
                    ConstantIndex(
                        constants,
                        GetNumericConstant(node, "gain")),
                    ConstantIndex(
                        constants,
                        GetNumericConstant(node, "offset")),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.If =>
                new(
                    FlowOpcode.If,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "condition"),
                    InputSlot(source, slots, nodeId, "whenTrue"),
                    InputSlot(source, slots, nodeId, "whenFalse"),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Selector =>
                new(
                    FlowOpcode.Selector,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "condition"),
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Sequence =>
                new(
                    FlowOpcode.Sequence,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "a"),
                    InputSlot(source, slots, nodeId, "b"),
                    ushort.MaxValue,
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Delay or FlowNodeKind.Timer =>
                new(
                    FlowOpcode.OnDelay,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "input"),
                    ushort.MaxValue,
                    stateSlots[nodeId],
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Pulse =>
                new(
                    FlowOpcode.RisingEdge,
                    resultSlotIndex,
                    InputSlot(source, slots, nodeId, "input"),
                    ushort.MaxValue,
                    stateSlots[nodeId],
                    nodeId,
                    NodeInstructionRole.Primary),

            FlowNodeKind.Schedule or FlowNodeKind.Calendar =>
                new(
                    FlowOpcode.DigitalConstant,
                    resultSlotIndex,
                    ushort.MaxValue,
                    ushort.MaxValue,
                    ConstantIndex(
                        constants,
                        GetBooleanConstant(
                            node.Configuration["enabled"].GetBoolean())),
                    nodeId,
                    NodeInstructionRole.Primary),

            _ => throw new UnreachableException()
        };
    }

    private static ushort InputSlot(
        ExecutableFlowSource source,
        Dictionary<string, ushort> slots,
        string targetId,
        string portId) => slots[source.Connections.Single(connection =>
            connection.Target.NodeId == targetId && connection.Target.PortId == portId).Source.NodeId];

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
    private static byte[] EncodeV1Instruction(V1Instruction instruction) => Concat(
        [(byte)instruction.Opcode, 0],
        U16(instruction.ResultSlotIndex),
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
        var entry = node.Kind == FlowNodeKind.FlowInput
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
        if (node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput)
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
        else if (node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput)
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
        else if (node.Kind == FlowNodeKind.DigitalConstant)
        {
            if (node.Configuration.Count != 1
                || !node.Configuration.TryGetValue("value", out var value)
                || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Failure("invalid_configuration", path, "A Boolean value is required.");
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
                throw Failure("invalid_configuration", path, "A supported comparison operator is required.");
            }
        }
        else if (node.Kind is FlowNodeKind.LevelShifter or FlowNodeKind.Line)
        {
            if (node.Configuration.Count != 2)
            {
                throw Failure("invalid_configuration", path, "Finite gain and offset values are required.");
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
                throw Failure("invalid_configuration", path, "Timer duration must be from 0 through 4294967295 milliseconds.");
            }
        }
        else if (node.Kind == FlowNodeKind.Clamp)
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
        else if (node.Kind is FlowNodeKind.Schedule or FlowNodeKind.Calendar)
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

    private static ConstantRecord GetBooleanConstant(bool value) => new(DataType.Boolean, value ? 1D : 0D);

    private static ConstantRecord GetNumericConstant(ExecutableFlowNode node, string key) =>
        new(DataType.Number, node.Configuration[key].GetDouble());

    private static ushort ConstantIndex(ConstantRecord[] constants, ConstantRecord value) =>
        checked((ushort)Array.IndexOf(constants, value));

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

    private static DataType ResultDataType(ExecutableFlowSource source, ExecutableFlowNode node) =>
        node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput ? InterfaceDataType(source, node)
        : node.Kind is FlowNodeKind.NumericConstant or FlowNodeKind.Add or FlowNodeKind.LevelShifter or FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput or
            FlowNodeKind.Average or FlowNodeKind.Calculator or FlowNodeKind.Clamp or FlowNodeKind.Min or FlowNodeKind.Max or FlowNodeKind.Line or FlowNodeKind.Selector ? DataType.Number : DataType.Boolean;

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
        foreach (var node in nodes.Where(node => node.Kind is FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogOutput))
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
                        : "pointId"].GetString()!,

                // Direction
                checked(node.Kind.ToString().EndsWith("anput", StringComparison.OrdinalIgnoreCase)
                    ? DataDirection.Input
                    : DataDirection.Output),

                // Type
                node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput
                    ? InterfaceDataType(source, node)
                    : node.Kind.ToString().StartsWith("analog", StringComparison.OrdinalIgnoreCase)
                        ? DataType.Number
                        : DataType.Boolean,

                // Units
                node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput
                    ? InterfaceUnits(source, node)
                    : PointUnits(node, resolvedPoints),

                // Kind
                node.Kind is FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput ? node.Kind : FlowNodeKind.Unknown))
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

    // Encode a required identifier/string as [length:u8][UTF-8 bytes].
    // The length is BYTE length, not C# char count; this matters for non-ASCII text.
    private static byte[] String8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat([checked((byte)bytes.Length)], bytes);
    }

    // Same physical string8 representation, but permits a zero length byte.
    private static byte[] String8AllowEmpty(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat([checked((byte)bytes.Length)], bytes);
    }

    // Materialize a u16 as two little-endian bytes. Returning byte[] makes field
    // composition via Concat visibly match the binary record diagrams above.
    private static byte[] U16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
        return bytes;
    }

    // Materialize a u32 as four little-endian bytes.
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
                    throw Failure("unit_mismatch", $"/nodes/{Escape(id)}", "Analog output units do not match its point binding.");
                }
            }

            if (node.Kind == FlowNodeKind.FlowOutput)
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
    private static void WriteU16(byte[] target, int offset, int value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset), checked((ushort)value));

    // In-place fixed-offset envelope writer for four-byte little-endian fields.
    private static void WriteU32(byte[] target, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);

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

    private static void ValidateIdentifier(string value, string path, int maximumBytes)
    {
        if (!IdentifierRegex().IsMatch(value) || Encoding.UTF8.GetByteCount(value) > maximumBytes)
        {
            throw Failure("invalid_identifier", path, "Identifier has invalid syntax or length.");
        }
    }

    private static FlowCompilationException Failure(string code, string path, string message) =>
        new([new FlowCompilationDiagnostic(code, path, message)]);

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
    private static string Escape(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
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
        ? CaptialCaseBoundaryRegex().Replace(node.Kind.ToString(), " $1").Trim() switch
        {
            var value when value.Length > 0 => char.ToUpperInvariant(value[0]) + value[1..],
            _ => node.Kind.ToString()
        }
        : node.Label;

    [GeneratedRegex("([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex CaptialCaseBoundaryRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    private sealed record FlowNodeShape(IReadOnlyList<FlowPort> Ports);

    private sealed record FlowPort(string Id, DataDirection Direction, DataType DataType);

    private sealed record FlowPortKey(string NodeId, string PortId);

    private sealed record PointRecord(string Id, DataDirection Direction, DataType DataType, string? Units, FlowNodeKind Kind = 0);

    private sealed record InterfaceRecord(string Id, string Name, DataType DataType, string? Units, JsonElement? DefaultValue);

    private sealed record ConstantRecord(DataType DataType, double Number);

    private sealed record V1Instruction(
        FlowOpcode Opcode,
        ushort ResultSlotIndex,
        ushort Operand0,
        ushort Operand1,
        ushort Auxiliary,
        string NodeId,
        NodeInstructionRole Role);

    private sealed record V1Section(ushort Id, uint Count, byte[] Bytes, ushort Version = 1);
}
