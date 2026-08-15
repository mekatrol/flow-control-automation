using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Server.Services.Implementation;

internal sealed class ManagedFlowVirtualMachine : IFlowVirtualMachine
{
    private const ushort Unused = ushort.MaxValue;
    private readonly object _gate = new();
    private readonly byte _qualityPolicy;
    private readonly ConstantRecord[] _constants;
    private readonly Point[] _points;
    private readonly byte[] _slotTypes;
    private readonly Instruction[] _instructions;
    private readonly bool[] _initialState;
    private readonly ulong[] _timerDurations;
    private bool[] _currentState;
    private bool[] _stagedState;
    private bool[] _stagedStateValid;
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
        _points = image.Points;
        _slotTypes = [.. image.Slots.Select(item => item.Type)];
        _instructions = image.Instructions;
        var stateSlots = image.Slots.Where(item => item.Kind is 3 or 4 or 5).ToArray();
        var stateBase = stateSlots.Length == 0 ? image.Slots.Length : stateSlots.Min(item => item.Index);
        if (stateSlots.Select((item, index) => item.Index != stateBase + index).Any(invalid => invalid))
        {
            Fail(10, "/slots/state");
        }

        _initialState = [.. stateSlots.Select(item =>
            item.Kind == 3 && Constant(item.InitialConstant, 1).Boolean)];
        _timerDurations = [.. stateSlots.Select(item =>
            item.Kind == 4 ? checked((ulong)Constant(item.InitialConstant, 2).Number) : 0UL)];
        _currentState = [.. _initialState];
        _stagedState = new bool[stateSlots.Length];
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
                Fail(16, "/lifecycle");
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
            Fail(16, "/lifecycle");
        }

        if (inputs.Count > 64 || (_qualityPolicy == 1 && inputs.Any(item => item.TypedValue.Quality != "good")))
        {
            Fail(17, "/inputs");
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

        var commands = _instructions.Where(item => item.Opcode == 7).Select(item =>
        {
            var point = _points[item.Auxiliary];
            return new FlowVmCommand(point.Id, _slots[item.Result], point.BindingKind == 1);
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
            Fail(16, "/lifecycle");
        }

        var instruction = _instructions[_instructionPointer++];
        var a = instruction.Operand0 == Unused ? FlowVmValue.FromBoolean(false) : _slots[instruction.Operand0];
        var b = instruction.Operand1 == Unused ? FlowVmValue.FromBoolean(false) : _slots[instruction.Operand1];
        var quality = Worse(a.Quality, b.Quality);
        switch (instruction.Opcode)
        {
            case 1:
                var point = _points[instruction.Auxiliary];
                var input = _inputs.FirstOrDefault(item => item.PointId == point.Id && item.IsInterface == (point.BindingKind == 1));
                if (input is null)
                {
                    Fail(17, "/inputs");
                }

                var inputValue = input.TypedValue;
                if (Type(inputValue) != point.Type)
                {
                    Fail(17, "/inputs");
                }

                _slots[instruction.Result] = inputValue;
                break;
            case 2: _slots[instruction.Result] = Value(Constant(instruction.Auxiliary, 1)); break;
            case 3: _slots[instruction.Result] = FlowVmValue.FromBoolean(!a.Boolean, a.Quality); break;
            case 4: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean && b.Boolean, quality); break;
            case 5: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean || b.Boolean, quality); break;
            case 6: _slots[instruction.Result] = FlowVmValue.FromBoolean(_currentState[State(instruction.Auxiliary)]); break;
            case 7: _slots[instruction.Result] = a; break;
            case 8:
                var state = State(instruction.Auxiliary);
                _stagedState[state] = a.Boolean;
                _stagedStateValid[state] = true;
                break;
            case 9: _slots[instruction.Result] = FlowVmValue.FromBoolean(!(a.Boolean && b.Boolean), quality); break;
            case 10: _slots[instruction.Result] = FlowVmValue.FromBoolean(!(a.Boolean || b.Boolean), quality); break;
            case 11: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean != b.Boolean, quality); break;
            case 12: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Boolean == b.Boolean, quality); break;
            case 13: _slots[instruction.Result] = Value(Constant(instruction.Auxiliary, 2)); break;
            case 14: Number(instruction, a.Number + b.Number, quality); break;
            case 15:
                var compared = instruction.Auxiliary switch { 1 => a.Number < b.Number, 2 => a.Number <= b.Number, 3 => a.Number == b.Number, 4 => a.Number >= b.Number, 5 => a.Number > b.Number, 6 => a.Number != b.Number, _ => throw Error(12, "/instructions/comparison") };
                _slots[instruction.Result] = FlowVmValue.FromBoolean(compared, quality);
                break;
            case 16: Number(instruction, a.Number * Constant(instruction.Operand1, 2).Number + Constant(instruction.Auxiliary, 2).Number, a.Quality); break;
            case 17: _slots[instruction.Result] = FlowVmValue.FromBoolean(a.Quality == "good"); break;
            case 18: OnDelay(instruction, a); break;
            case 19: RisingEdge(instruction, a); break;
            case 20: Number(instruction, Math.Min(a.Number, b.Number), quality); break;
            case 21: Number(instruction, Math.Max(a.Number, b.Number), quality); break;
            case 22: Number(instruction, Math.Clamp(a.Number, Constant(instruction.Operand1, 2).Number, Constant(instruction.Auxiliary, 2).Number), a.Quality); break;
            case 23:
                var selected = a.Boolean ? instruction.Operand1 : instruction.Auxiliary;
                _slots[instruction.Result] = _slots[selected] with { Quality = Worse(a.Quality, _slots[selected].Quality) };
                break;
            case 24: _slots[instruction.Result] = a; break;
            case 255: break;
            default: Fail(11, "/instructions"); break;
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

    private void RisingEdge(Instruction instruction, FlowVmValue input)
    {
        var state = State(instruction.Auxiliary);
        _slots[instruction.Result] = FlowVmValue.FromBoolean(input.Boolean && !_currentState[state], input.Quality);
        _stagedState[state] = input.Boolean;
        _stagedStateValid[state] = true;
    }

    private void Number(Instruction instruction, double value, string quality)
    {
        if (!double.IsFinite(value))
        {
            Fail(17, "/arithmeticOverflow");
        }

        _slots[instruction.Result] = FlowVmValue.FromNumber(value, quality);
    }

    private FlowVmExecutionFrame Frame() => new(
        checked((ushort)_instructionPointer),
        _instructionPointer >= _instructions.Length ? byte.MaxValue : _instructions[_instructionPointer].Opcode,
        _instructionPointer >= _instructions.Length,
        [.. _slots],
        [.. _currentState],
        [.. _stagedState.Select((value, index) => _stagedStateValid[index] ? (bool?)value : null)],
        []);

    private FlowVmValue[] EmptySlots() => [.. _slotTypes.Select(type =>
        type == 2 ? FlowVmValue.FromNumber(0) : FlowVmValue.FromBoolean(false))];
    private ConstantRecord Constant(int index, byte type) => index >= 0 && index < _constants.Length && _constants[index].Type == type ? _constants[index] : throw Error(12, "/instructions/constant");
    private int State(int slot) => slot >= StateBase && slot - StateBase < _currentState.Length ? slot - StateBase : throw Error(12, "/instructions/state");
    private static byte Type(FlowVmValue value) => value.Type == "number" ? (byte)2 : (byte)1;
    private static FlowVmValue Value(ConstantRecord value) => value.Type == 2 ? FlowVmValue.FromNumber(value.Number) : FlowVmValue.FromBoolean(value.Boolean);
    private static string Worse(string left, string right) => left == "good" && right == "good" ? "good" : "bad";
    private void RequireExecuting()
    {
        ThrowIfDisposed(); if (!_executing)
        {
            Fail(16, "/lifecycle");
        }
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private static FlowVirtualMachineException Error(int code, string path) => new(code, path);
    [DoesNotReturn]
    private static void Fail(int code, string path) => throw Error(code, path);

    private sealed record ConstantRecord(byte Type, bool Boolean, double Number);
    private sealed record Point(byte Direction, byte Type, byte BindingKind, string Id);
    private sealed record Slot(byte Kind, byte Type, ushort Index, ushort InitialConstant);
    private sealed record Instruction(byte Opcode, ushort Result, ushort Operand0, ushort Operand1, ushort Auxiliary);

    private sealed record Image(byte QualityPolicy, ConstantRecord[] Constants, Point[] Points, Slot[] Slots, Instruction[] Instructions)
    {
        public static Image Parse(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length is < 512 or > 16384 || !bytes[..4].SequenceEqual("FIL1"u8))
            {
                Fail(1, "/");
            }

            if (U16(bytes, 4) != 1)
            {
                Fail(2, "/version");
            }

            if (U16(bytes, 6) != 128 || U32(bytes, 8) != bytes.Length || U16(bytes, 26) != 8)
            {
                Fail(3, "/envelope");
            }

            var sections = new Section[8];
            for (var index = 0; index < sections.Length; index++)
            {
                var entry = bytes.Slice(128 + index * 48, 48);
                if (U16(entry, 0) != index + 1)
                {
                    Fail(5, $"/sections/{index}");
                }

                var offset = checked((int)U32(entry, 4));
                var length = checked((int)U32(entry, 8));
                if (offset < 512 || length < 0 || offset > bytes.Length - length)
                {
                    Fail(3, $"/sections/{index}");
                }

                sections[index] = new(offset, length, checked((int)U32(entry, 12)));
            }
            var constants = ReadConstants(bytes, sections[0]);
            var points = ReadPoints(bytes, sections[1]);
            var slots = Fixed(bytes, sections[2], 8).Select(record => new Slot(record[0], record[1], U16(record, 4), U16(record, 6))).ToArray();
            var instructions = Fixed(bytes, sections[3], 12).Select(record => new Instruction(record[0], U16(record, 2), U16(record, 4), U16(record, 6), U16(record, 8))).ToArray();
            var idLength = bytes[52];
            if (idLength is 0 or > 63)
            {
                Fail(7, "/flowId");
            }

            _ = Encoding.UTF8.GetString(bytes.Slice(53, idLength));
            return new(bytes[28], constants, points, slots, instructions);
        }

        private static ConstantRecord[] ReadConstants(ReadOnlySpan<byte> bytes, Section section)
        {
            var result = new List<ConstantRecord>();
            var offset = section.Offset;
            for (var index = 0; index < section.Count; index++)
            {
                var type = bytes[offset];
                var flags = bytes[offset + 1];
                offset += 4;
                if (type == 1)
                {
                    result.Add(new(1, flags == 1, 0));
                }
                else if (type == 2)
                {
                    var number = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(offset, 8)));
                    if (!double.IsFinite(number))
                    {
                        Fail(8, $"/constants/{index}");
                    }

                    result.Add(new(2, false, number));
                    offset += 8;
                }
                else
                {
                    Fail(8, $"/constants/{index}");
                }
            }
            if (offset != section.Offset + section.Length)
            {
                Fail(3, "/constants");
            }

            return [.. result];
        }

        private static Point[] ReadPoints(ReadOnlySpan<byte> bytes, Section section)
        {
            var result = new List<Point>();
            var offset = section.Offset;
            for (var index = 0; index < section.Count; index++)
            {
                var direction = bytes[offset++];
                var type = bytes[offset++];
                _ = bytes[offset++];
                var binding = bytes[offset++];
                var id = String8(bytes, ref offset);
                _ = String8(bytes, ref offset, allowEmpty: true);
                result.Add(new(direction, type, binding, id));
            }
            if (offset != section.Offset + section.Length)
            {
                Fail(3, "/points");
            }

            return [.. result];
        }

        private static byte[][] Fixed(ReadOnlySpan<byte> bytes, Section section, int size)
        {
            if (section.Length != section.Count * size)
            {
                Fail(3, "/sections");
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
                Fail(7, "/identifier");
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