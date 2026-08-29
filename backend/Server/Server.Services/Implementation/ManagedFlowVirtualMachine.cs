using Server.Common.Contracts;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Server.Services.Implementation;

internal sealed class ManagedFlowVirtualMachine : IFlowVirtualMachine
{
    private const ushort Unused = ushort.MaxValue;
    private readonly Lock _gate = new();
    private readonly byte _qualityPolicy;
    private readonly ConstantRecord[] _constants;
    private readonly Point[] _points;
    private readonly DataType[] _slotDataTypes;
    private readonly Instruction[] _instructions;
    private readonly FlowVmValue[] _initialState;
    private readonly ulong[] _timerDurations;
    private FlowVmValue[] _currentState;
    private FlowVmValue[] _stagedState;
    private readonly bool[] _stagedStateValid;
    private ulong[] _timerStartedAt;
    private ulong[] _stagedTimerStartedAt;
    private FlowVmValue[] _slots;
    private IReadOnlyList<FlowVmInput> _inputs = [];
    private ulong _sampledAt;
    private ulong _scanNumber;
    private int _instructionPointer;
    private bool _executing;
    private bool _disposed;

    public ManagedFlowVirtualMachine(ReadOnlyMemory<byte> artifact)
    {
        var image = Image.Parse(artifact.Span);
        _qualityPolicy = image.QualityPolicy;
        _constants = image.Constants;
        _points = [.. image.Points];
        _slotDataTypes = [.. image.Slots.Select(item => item.DataType)];
        _instructions = image.Instructions;
        var stateSlots = image.Slots.Where(item => item.Kind is FlowSlotKind.MemoryState or FlowSlotKind.TimerState or FlowSlotKind.EdgeState).ToArray();
        var stateBase = stateSlots.Length == 0 ? image.Slots.Length : stateSlots.Min(item => item.Index);

        if (stateSlots.Select((item, index) => item.Index != stateBase + index).Any(invalid => invalid))
        {
            Fail(FlowVmErrorCode.InvalidStateLayout, "/slots/state");
        }

        _initialState =
            [
                .. stateSlots.Select(item => item.Kind switch
                {
                    FlowSlotKind.MemoryState => FlowVmValue.FromNumber(Constant(item.InitialConstant, DataType.Number).Number),
                    FlowSlotKind.EdgeState => FlowVmValue.FromBoolean(false),
                    FlowSlotKind.TimerState => FlowVmValue.FromBoolean(false),
                    _ => throw Error(FlowVmErrorCode.InvalidInstruction, "/slots/state")
                })
            ];

        _timerDurations = [.. stateSlots.Select(item => item.Kind == FlowSlotKind.TimerState ? checked((ulong)Constant(item.InitialConstant, DataType.Number).Number) : 0UL)];
        _currentState = [.. _initialState];
        _stagedState = new FlowVmValue[stateSlots.Length];
        _stagedStateValid = new bool[stateSlots.Length];
        _timerStartedAt = new ulong[stateSlots.Length];
        _stagedTimerStartedAt = new ulong[stateSlots.Length];
        _slots = EmptySlots();
        StateBase = stateBase;
    }

    private int StateBase { get; }

    public FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
    {
        lock (_gate)
        {
            BeginScanCore(inputs, sampledAtMilliseconds);
            return CommitScanCore();
        }
    }

    public FlowVmExecutionFrame BeginScan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
    {
        lock (_gate)
        {
            BeginScanCore(inputs, sampledAtMilliseconds);
            return Frame();
        }
    }

    public FlowVmExecutionFrame StepInstruction()
    {
        lock (_gate)
        {
            RequireExecuting();
            ExecuteNext();
            return Frame();
        }
    }

    public FlowVmScanResult CommitScan()
    {
        lock (_gate)
        {
            RequireExecuting();
            return CommitScanCore();
        }
    }

    public void AbortScan()
    {
        lock (_gate)
        {
            RequireExecuting();
            _executing = false;
            _inputs = [];
            _slots = EmptySlots();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_executing)
            {
                Fail(FlowVmErrorCode.InvalidLifecycleState, "/lifecycle");
            }

            _currentState = [.. _initialState];
            Array.Clear(_timerStartedAt);
            _scanNumber = 0;
            _slots = EmptySlots();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }

    private void BeginScanCore(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ThrowIfDisposed();
        if (_executing)
        {
            Fail(FlowVmErrorCode.InvalidLifecycleState, "/lifecycle");
        }

        if (inputs.Count > 64 || (_qualityPolicy == 1 && inputs.Any(item => item.TypedValue.Quality != DataQuality.Good)))
        {
            Fail(FlowVmErrorCode.InvalidRuntimeInput, "/inputs");
        }

        _inputs = inputs;
        _sampledAt = sampledAtMilliseconds;
        _instructionPointer = 0;
        _slots = EmptySlots();
        _stagedState = [.. _currentState];
        Array.Clear(_stagedStateValid);
        _stagedTimerStartedAt = [.. _timerStartedAt];
        _executing = true;
    }

    private FlowVmScanResult CommitScanCore()
    {
        while (_instructionPointer < _instructions.Length)
        {
            ExecuteNext();
        }

        var commands = _instructions.Where(item => item.Opcode == FlowOpcode.PointOutput).Select(item =>
        {
            var point = _points[item.Auxiliary];
            return new FlowVmCommand(point.Id, _slots[item.Result]);
        }).ToArray();

        _currentState = [.. _stagedState];
        _timerStartedAt = [.. _stagedTimerStartedAt];
        _scanNumber++;
        _executing = false;
        _inputs = [];
        return new FlowVmScanResult(_scanNumber, _sampledAt, [.. _slots], commands);
    }

    private void ExecuteNext()
    {
        RequireExecuting();
        if (_instructionPointer >= _instructions.Length)
        {
            Fail(FlowVmErrorCode.InvalidLifecycleState, "/lifecycle");
        }

        var instruction = _instructions[_instructionPointer++];
        var a = instruction.Operand0 == Unused ? FlowVmValue.FromBoolean(false) : _slots[instruction.Operand0];
        var b = instruction.Operand1 == Unused || instruction.Opcode == FlowOpcode.D2A
            ? FlowVmValue.FromBoolean(false)
            : _slots[instruction.Operand1];
        var quality = Worse(a.Quality, b.Quality);
        switch (instruction.Opcode)
        {
            case FlowOpcode.PointInput:
                var point = _points[instruction.Auxiliary];
                var input = _inputs.FirstOrDefault(item => item.PointId == point.Id);

                if (input is null)
                {
                    Fail(FlowVmErrorCode.InvalidRuntimeInput, "/inputs");
                }

                var inputValue = input.TypedValue;

                if (inputValue.DataType != point.DataType)
                {
                    Fail(FlowVmErrorCode.InvalidRuntimeInput, "/inputs");
                }

                _slots[instruction.Result] = inputValue;
                break;
            case FlowOpcode.DigitalConstant:
                _slots[instruction.Result] =
                    Value(Constant(instruction.Auxiliary, DataType.Boolean));
                break;
            case FlowOpcode.Not: _slots[instruction.Result] = FlowVmValue.FromBoolean(!a.Boolean, a.Quality); break;
            case FlowOpcode.And: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean && b.Boolean, quality); break;
            case FlowOpcode.Or: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean || b.Boolean, quality); break;
            case FlowOpcode.Memory:
                _slots[instruction.Result] = _currentState[State(instruction.Auxiliary)];
                break;
            case FlowOpcode.PointOutput: _slots[instruction.Result] = a; break;
            case FlowOpcode.MemoryCommit:
                var state = State(instruction.Auxiliary);
                _stagedState[state] = a;
                _stagedStateValid[state] = true;
                break;
            case FlowOpcode.Nand: _slots[instruction.Result] = FlowVmValue.FromBoolean(!(a.Boolean && b.Boolean), quality); break;
            case FlowOpcode.Nor: _slots[instruction.Result] = FlowVmValue.FromBoolean(!(a.Boolean || b.Boolean), quality); break;
            case FlowOpcode.Xor: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean != b.Boolean, quality); break;
            case FlowOpcode.Xnor: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean == b.Boolean, quality); break;
            case FlowOpcode.NumericConstant:
                _slots[instruction.Result] =
                    Value(Constant(instruction.Auxiliary, DataType.Number));
                break;
            case FlowOpcode.Add: Number(instruction, a.Number + b.Number, quality); break;
            case FlowOpcode.Subtract: Number(instruction, a.Number - b.Number, quality); break;
            case FlowOpcode.Multiply: Number(instruction, a.Number * b.Number, quality); break;
            case FlowOpcode.Divide: Number(instruction, a.Number / b.Number, quality); break;
            case FlowOpcode.Power: Number(instruction, Math.Pow(a.Number, b.Number), quality); break;
            case FlowOpcode.Negate: Number(instruction, -a.Number, a.Quality); break;
            case FlowOpcode.Calculator: _slots[instruction.Result] = a; break;
            case FlowOpcode.CalculatorInputs: break;
            case FlowOpcode.Average: Number(instruction, (a.Number / 2D) + (b.Number / 2D), quality); break;
            case FlowOpcode.Comparator:
                var compared = instruction.Auxiliary switch { 1 => a.Number < b.Number, 2 => a.Number <= b.Number, 3 => a.Number == b.Number, 4 => a.Number >= b.Number, 5 => a.Number > b.Number, 6 => a.Number != b.Number, _ => throw Error(FlowVmErrorCode.InvalidInstruction, "/instructions/comparison") };
                _slots[instruction.Result] = FlowVmValue.FromBoolean(compared, quality);
                break;
            case FlowOpcode.LevelShifter:
                Number(
                    instruction,
                    (a.Number * Constant(instruction.Operand1, DataType.Number).Number) + Constant(instruction.Auxiliary, DataType.Number).Number,
                    a.Quality);
                break;
            case FlowOpcode.QualityGood: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Quality == DataQuality.Good); break;
            case FlowOpcode.OnDelay: OnDelay(instruction, a); break;
            case FlowOpcode.RisingEdge: RisingEdge(instruction, a); break;
            case FlowOpcode.Min: Number(instruction, Math.Min(a.Number, b.Number), quality); break;
            case FlowOpcode.Max: Number(instruction, Math.Max(a.Number, b.Number), quality); break;
            case FlowOpcode.Clamp:
                Number(
                    instruction,
                    Math.Clamp(
                        a.Number,
                        Constant(instruction.Operand1, DataType.Number).Number,
                        Constant(instruction.Auxiliary, DataType.Number).Number),
                    a.Quality);
                break;
            case FlowOpcode.Switch:
                var selected = a.Boolean ? instruction.Operand1 : instruction.Auxiliary;
                _slots[instruction.Result] = _slots[selected] with { Quality = Worse(a.Quality, _slots[selected].Quality) };
                break;
            case FlowOpcode.Passthrough: _slots[instruction.Result] = a; break;
            case FlowOpcode.A2DLow:
                var lowState = State(instruction.Operand1);
                var lowValue = a.Number > Constant(instruction.Auxiliary, DataType.Number).Number
                    && _currentState[lowState].Boolean;
                _slots[instruction.Result] = FlowVmValue.FromBoolean(lowValue, a.Quality);
                break;
            case FlowOpcode.A2DHigh:
                var highState = State(instruction.Operand1);
                var highValue = a.Number >= Constant(instruction.Auxiliary, DataType.Number).Number
                    || _slots[instruction.Result].Boolean;
                _slots[instruction.Result] = FlowVmValue.FromBoolean(highValue, a.Quality);
                _stagedState[highState] = FlowVmValue.FromBoolean(highValue, a.Quality);
                _stagedStateValid[highState] = true;
                break;
            case FlowOpcode.D2A:
                var analogValue = Constant(
                    a.Boolean ? instruction.Auxiliary : instruction.Operand1,
                    DataType.Number).Number;
                _slots[instruction.Result] = FlowVmValue.FromNumber(analogValue, a.Quality);
                break;
            case FlowOpcode.Commit: break;
            default: Fail(FlowVmErrorCode.InvalidOpcode, "/instructions"); break;
        }
    }

    private void OnDelay(Instruction instruction, FlowVmValue input)
    {
        var state = State(instruction.Auxiliary);
        if (!input.Boolean)
        {
            _slots[instruction.Result] = FlowVmValue.FromBoolean(false, input.Quality);
            _stagedTimerStartedAt[state] = 0;
        }
        else
        {
            if (_timerStartedAt[state] == 0)
            {
                _stagedTimerStartedAt[state] = _sampledAt == 0 ? 1UL : _sampledAt;
            }

            var started = _timerStartedAt[state] == 0 ? _stagedTimerStartedAt[state] : _timerStartedAt[state];
            _slots[instruction.Result] = FlowVmValue.FromBoolean(_sampledAt >= started && _sampledAt - started >= _timerDurations[state], input.Quality);
        }
        _stagedStateValid[state] = true;
    }

    private void RisingEdge(
        Instruction instruction,
        FlowVmValue input)
    {
        var state = State(instruction.Auxiliary);

        _slots[instruction.Result] = FlowVmValue.FromBoolean(
            input.Boolean && !_currentState[state].Boolean,
            input.Quality);

        _stagedState[state] = FlowVmValue.FromBoolean(input.Boolean);
        _stagedStateValid[state] = true;
    }

    private void Number(Instruction instruction, double value, DataQuality quality)
    {
        if (!double.IsFinite(value))
        {
            Fail(FlowVmErrorCode.InvalidRuntimeInput, "/arithmeticOverflow");
        }

        _slots[instruction.Result] = FlowVmValue.FromNumber(value, quality);
    }

    private FlowVmExecutionFrame Frame() => new(
        checked((ushort)_instructionPointer),
        _instructionPointer >= _instructions.Length
            ? FlowOpcode.Commit
            : _instructions[_instructionPointer].Opcode,
        _instructionPointer >= _instructions.Length,
        [.. _slots],
        [.. _currentState.Select(value => value.Boolean)],
        [.. _stagedState.Select((value, index) =>
        _stagedStateValid[index]
            ? value.Boolean
            : (bool?)null)],
        []);

    private FlowVmValue[] EmptySlots() => [.. _slotDataTypes.Select(type => type == DataType.Number ? FlowVmValue.FromNumber(0) : FlowVmValue.FromBoolean(false))];

    private ConstantRecord Constant(int index, DataType type) => index >= 0 && index < _constants.Length && _constants[index].DataType == type ? _constants[index] : throw Error(FlowVmErrorCode.InvalidInstruction, "/instructions/constant");

    private int State(int slot) => slot >= StateBase && slot - StateBase < _currentState.Length ? slot - StateBase : throw Error(FlowVmErrorCode.InvalidInstruction, "/instructions/state");

    private static FlowVmValue Value(ConstantRecord value) =>
        value.DataType == DataType.Number
            ? FlowVmValue.FromNumber(value.Number)
            : FlowVmValue.FromBoolean(value.Boolean);

    private static DataQuality Worse(DataQuality left, DataQuality right) => left == DataQuality.Good && right == DataQuality.Good ? DataQuality.Good : DataQuality.Bad;

    private void RequireExecuting()
    {
        ThrowIfDisposed(); if (!_executing)
        {
            Fail(FlowVmErrorCode.InvalidLifecycleState, "/lifecycle");
        }
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static FlowVmException Error(FlowVmErrorCode code, string path) => new(code, path);

    [DoesNotReturn]
    private static void Fail(FlowVmErrorCode code, string path) => throw Error(code, path);

    private sealed record ConstantRecord(DataType DataType, bool Boolean, double Number);

    private sealed record Point(DataDirection Direction, DataType DataType, PointBindingKind BindingKind, string Id);

    private sealed record Slot(FlowSlotKind Kind, DataType DataType, ushort Index, ushort InitialConstant);

    private sealed record Instruction(FlowOpcode Opcode, ushort Result, ushort Operand0, ushort Operand1, ushort Auxiliary);

    private sealed record Image(byte QualityPolicy, ConstantRecord[] Constants, Point[] Points, Slot[] Slots, Instruction[] Instructions)
    {
        public static Image Parse(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length is < 512 or > 16384 || !bytes[..4].SequenceEqual("FIL1"u8))
            {
                Fail(FlowVmErrorCode.InvalidImage, "/");
            }

            if (U16(bytes, 4) != 1)
            {
                Fail(FlowVmErrorCode.UnsupportedVersion, "/version");
            }

            if (U16(bytes, 6) != 128 || U32(bytes, 8) != bytes.Length || U16(bytes, 26) != 8)
            {
                Fail(FlowVmErrorCode.InvalidEnvelope, "/envelope");
            }

            var sections = new Section[8];
            for (var index = 0; index < sections.Length; index++)
            {
                var entry = bytes.Slice(128 + (index * 48), 48);
                if (U16(entry, 0) != index + 1)
                {
                    Fail(FlowVmErrorCode.InvalidSection, $"/sections/{index}");
                }

                var offset = checked((int)U32(entry, 4));
                var length = checked((int)U32(entry, 8));
                if (offset < 512 || length < 0 || offset > bytes.Length - length)
                {
                    Fail(FlowVmErrorCode.InvalidEnvelope, $"/sections/{index}");
                }

                sections[index] = new(offset, length, checked((int)U32(entry, 12)));
            }
            var constants = ReadConstants(bytes, sections[0]);
            var points = ReadPoints(bytes, sections[1]);
            var slots = Fixed(bytes, sections[2], 8).Select(record => new Slot((FlowSlotKind)record[0], (DataType)record[1], U16(record, 4), U16(record, 6))).ToArray();
            var instructions = Fixed(bytes, sections[3], 12).Select(record => new Instruction((FlowOpcode)record[0], U16(record, 2), U16(record, 4), U16(record, 6), U16(record, 8))).ToArray();
            var idLength = bytes[52];
            if (idLength is 0 or > 63)
            {
                Fail(FlowVmErrorCode.InvalidIdentifier, "/flowId");
            }

            _ = Encoding.UTF8.GetString(bytes.Slice(53, idLength));
            return new(bytes[28], constants, points, slots, instructions);
        }

        private static ConstantRecord[] ReadConstants(
            ReadOnlySpan<byte> bytes,
            Section section)
        {
            var result = new List<ConstantRecord>();
            var offset = section.Offset;

            for (var index = 0; index < section.Count; index++)
            {
                var type = (DataType)bytes[offset];
                var flags = bytes[offset + 1];

                offset += 4;

                if (type == DataType.Boolean)
                {
                    result.Add(new(
                        DataType.Boolean,
                        flags == 1,
                        0));
                }
                else if (type == DataType.Number)
                {
                    var number = BitConverter.Int64BitsToDouble(
                        BinaryPrimitives.ReadInt64LittleEndian(
                            bytes.Slice(offset, 8)));

                    if (!double.IsFinite(number))
                    {
                        Fail(FlowVmErrorCode.InvalidConstant, $"/constants/{index}");
                    }

                    result.Add(new(
                        DataType.Number,
                        false,
                        number));

                    offset += 8;
                }
                else
                {
                    Fail(FlowVmErrorCode.InvalidConstant, $"/constants/{index}");
                }
            }

            if (offset != section.Offset + section.Length)
            {
                Fail(FlowVmErrorCode.InvalidEnvelope, "/constants");
            }

            return [.. result];
        }

        private static Point[] ReadPoints(ReadOnlySpan<byte> bytes, Section section)
        {
            var result = new List<Point>();
            var offset = section.Offset;
            for (var index = 0; index < section.Count; index++)
            {
                var direction = (DataDirection)bytes[offset++];
                var type = (DataType)bytes[offset++];
                _ = bytes[offset++];
                var binding = (PointBindingKind)bytes[offset++];
                var id = String8(bytes, ref offset);
                _ = String8(bytes, ref offset, allowEmpty: true);
                result.Add(new(direction, type, binding, id));
            }

            if (offset != section.Offset + section.Length)
            {
                Fail(FlowVmErrorCode.InvalidEnvelope, "/points");
            }

            return [.. result];
        }

        private static byte[][] Fixed(ReadOnlySpan<byte> bytes, Section section, int size)
        {
            if (section.Length != section.Count * size)
            {
                Fail(FlowVmErrorCode.InvalidEnvelope, "/sections");
            }

            var copy = bytes.Slice(section.Offset, section.Length).ToArray();
            return [.. Enumerable.Range(0, section.Count)
                .Select(index => copy[(index * size)..((index + 1) * size)])];
        }

        private static string String8(ReadOnlySpan<byte> bytes, ref int offset, bool allowEmpty = false)
        {
            var length = bytes[offset++];
            if (!allowEmpty && length == 0)
            {
                Fail(FlowVmErrorCode.InvalidIdentifier, "/identifier");
            }

            var value = Encoding.UTF8.GetString(bytes.Slice(offset, length));
            offset += length;
            return value;
        }

        private sealed record Section(int Offset, int Length, int Count);

        private static ushort U16(ReadOnlySpan<byte> value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value[offset..]);

        private static uint U32(ReadOnlySpan<byte> value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value[offset..]);
    }
}