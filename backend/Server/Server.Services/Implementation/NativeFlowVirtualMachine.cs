using Microsoft.Win32.SafeHandles;
using Server.Services.Contracts;
using System.Runtime.InteropServices;
using System.Text;

namespace Server.Services.Implementation;

internal sealed unsafe partial class NativeFlowVirtualMachine : IFlowVirtualMachine
{
    private const uint AbiVersion = 2;
    private const int MaximumArtifactBytes = 16384;
    private const int MaximumSlots = 256;
    private const int MaximumStates = 128;
    private const int MaximumOutputs = 64;
    private readonly object _gate = new();
    private NativeVmHandle _instance;
    private bool _disposed;

    public NativeFlowVirtualMachine(ReadOnlyMemory<byte> artifact)
    {
        if (artifact.Length is <= 0 or > MaximumArtifactBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(artifact));
        }

        if (Native.flow_vm_get_abi_version() != AbiVersion)
        {
            throw new FlowVirtualMachineException(2, "/abiVersion");
        }

        var instanceBytes = Native.flow_vm_get_instance_size();
        if (instanceBytes == 0 || instanceBytes > 1024 * 1024)
        {
            throw new FlowVirtualMachineException(6, "/instanceBytes");
        }

        _instance = new NativeVmHandle(Marshal.AllocHGlobal(checked((int)instanceBytes)));
        new Span<byte>((void*)Handle, checked((int)instanceBytes)).Clear();
        try
        {
            var target = new NativeTarget
            {
                AbiVersion = AbiVersion,
                Capabilities = 0xfff,
                MaximumArtifactBytes = MaximumArtifactBytes,
                MaximumWorkPerScan = 256,
                MaximumSnapshotBytes = 16384
            };
            fixed (byte* artifactPointer = artifact.Span)
            {
                Check(Native.flow_vm_prepare(artifactPointer, (nuint)artifact.Length, &target, Handle));
            }

            Check(Native.flow_vm_initialize(Handle, null, 0));
        }
        catch
        {
            _instance.Dispose();
            throw;
        }
    }

    public FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count > 64) throw new ArgumentOutOfRangeException(nameof(inputs));
        lock (_gate)
        {
            ThrowIfDisposed();
            var samples = stackalloc NativeInput[inputs.Count];
            for (var index = 0; index < inputs.Count; index++)
            {
                WriteIdentifier(inputs[index].PointId, samples[index].PointId, 64);
                samples[index].Value = inputs[index].Value ? (byte)1 : (byte)0;
                samples[index].Quality = inputs[index].IsGood ? (byte)0 : (byte)1;
                samples[index].Type = 1;
                if (inputs[index].TypedValue.Type == "number")
                {
                    samples[index].Type = 2;
                    samples[index].Number = inputs[index].TypedValue.Number;
                }
            }

            var frame = new NativeInputFrame
            {
                Samples = (IntPtr)samples,
                SampleCount = (nuint)inputs.Count,
                SampledAtMilliseconds = sampledAtMilliseconds,
                IsCoherent = 1
            };
            Check(Native.flow_vm_begin_tick(Handle, &frame));
            var commands = stackalloc NativeCommand[MaximumOutputs];
            nuint commandCount = 0;
            NativeSnapshot snapshot = default;
            try
            {
                Check(Native.flow_vm_commit_tick(
                    Handle,
                    commands,
                    MaximumOutputs,
                    &commandCount,
                    &snapshot));
            }
            catch
            {
                _ = Native.flow_vm_abort_tick(Handle);
                throw;
            }

            var resultCommands = new FlowVmCommand[checked((int)commandCount)];
            for (var index = 0; index < resultCommands.Length; index++)
            {
                resultCommands[index] = ReadCommand(commands[index]);
            }

            var slots = ReadSlots(snapshot.SlotCount, snapshot.Slots, snapshot.SlotTypes, snapshot.SlotQualities, snapshot.NumericSlots);
            return new FlowVmScanResult(snapshot.ScanNumber, snapshot.SampledAtMilliseconds, slots, resultCommands);
        }
    }

    public FlowVmExecutionFrame BeginScan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count > 64) throw new ArgumentOutOfRangeException(nameof(inputs));
        lock (_gate)
        {
            ThrowIfDisposed();
            BeginScanCore(inputs, sampledAtMilliseconds);
            return GetFrameCore();
        }
    }

    public FlowVmExecutionFrame StepInstruction()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            NativeExecutionView view = default;
            Check(Native.flow_vm_step_instruction(Handle, &view));
            return GetFrameCore();
        }
    }

    public FlowVmScanResult CommitScan()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return CommitScanCore();
        }
    }

    public void AbortScan()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            Check(Native.flow_vm_abort_tick(Handle));
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            Check(Native.flow_vm_reset(Handle));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _instance.Dispose();
        }
    }

    private static void Check(NativeResult result)
    {
        if (result.Code != 0) throw new FlowVirtualMachineException(result.Code, ReadIdentifier(result.Path, 96));
    }

    private static void WriteIdentifier(string value, byte* target, int capacity)
    {
        var count = Encoding.UTF8.GetByteCount(value);
        if (count is <= 0 || count >= capacity) throw new ArgumentException("Identifier is outside native bounds.", nameof(value));
        fixed (char* chars = value)
        {
            _ = Encoding.UTF8.GetBytes(chars, value.Length, target, capacity - 1);
        }
        target[count] = 0;
    }

    private static string ReadIdentifier(byte* value, int capacity)
    {
        var length = 0;
        while (length < capacity && value[length] != 0) length++;
        return Encoding.UTF8.GetString(value, length);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private IntPtr Handle => _instance.DangerousGetHandle();

    private void BeginScanCore(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
    {
        var samples = stackalloc NativeInput[inputs.Count];
        for (var index = 0; index < inputs.Count; index++)
        {
            WriteIdentifier(inputs[index].PointId, samples[index].PointId, 64);
            samples[index].Value = inputs[index].Value ? (byte)1 : (byte)0;
            samples[index].Quality = inputs[index].IsGood ? (byte)0 : (byte)1;
            samples[index].Type = 1;
            if (inputs[index].TypedValue.Type == "number")
            {
                samples[index].Type = 2;
                samples[index].Number = inputs[index].TypedValue.Number;
            }
        }

        var frame = new NativeInputFrame
        {
            Samples = (IntPtr)samples,
            SampleCount = (nuint)inputs.Count,
            SampledAtMilliseconds = sampledAtMilliseconds,
            IsCoherent = 1
        };
        Check(Native.flow_vm_begin_tick(Handle, &frame));
    }

    private FlowVmScanResult CommitScanCore()
    {
        var commands = stackalloc NativeCommand[MaximumOutputs];
        nuint commandCount = 0;
        NativeSnapshot snapshot = default;
        Check(Native.flow_vm_commit_tick(Handle, commands, MaximumOutputs, &commandCount, &snapshot));
        var resultCommands = new FlowVmCommand[checked((int)commandCount)];
        for (var index = 0; index < resultCommands.Length; index++)
        {
            resultCommands[index] = ReadCommand(commands[index]);
        }

        var slots = ReadSlots(snapshot.SlotCount, snapshot.Slots, snapshot.SlotTypes, snapshot.SlotQualities, snapshot.NumericSlots);
        return new FlowVmScanResult(snapshot.ScanNumber, snapshot.SampledAtMilliseconds, slots, resultCommands);
    }

    private FlowVmExecutionFrame GetFrameCore()
    {
        NativeDebugFrame frame = default;
        Check(Native.flow_vm_get_debug_frame(Handle, &frame));
        var slots = ReadSlots(frame.SlotCount, frame.Slots, frame.SlotTypes, frame.SlotQualities, frame.NumericSlots);
        var currentState = new bool[frame.StateCount];
        var stagedState = new bool?[frame.StateCount];
        var commands = new FlowVmCommand[frame.OutputCount];
        for (var index = 0; index < currentState.Length; index++)
        {
            currentState[index] = frame.CurrentState[index] != 0;
            stagedState[index] = frame.StagedStateValid[index] != 0 ? frame.StagedState[index] != 0 : null;
        }
        for (var index = 0; index < commands.Length; index++)
        {
            commands[index] = ReadCommand(*(NativeCommand*)(frame.Outputs + (index * 80)));
        }
        return new FlowVmExecutionFrame(
            frame.Execution.InstructionIndex,
            frame.Execution.Opcode,
            frame.Execution.IsAtCommit != 0,
            slots,
            currentState,
            stagedState,
            commands);
    }

    private static FlowVmCommand ReadCommand(NativeCommand command)
    {
        var quality = command.Quality == 0 ? "good" : "bad";
        var value = command.Type == 2
            ? FlowVmValue.FromNumber(command.Number, quality)
            : FlowVmValue.FromBoolean(command.Value != 0, quality);
        return new FlowVmCommand(ReadIdentifier(command.PointId, 64), value);
    }

    private static FlowVmValue[] ReadSlots(ushort count, byte* booleans, byte* types, byte* qualities, double* numbers)
    {
        var result = new FlowVmValue[count];
        for (var index = 0; index < result.Length; index++)
        {
            var quality = qualities[index] == 0 ? "good" : "bad";
            result[index] = types[index] == 2
                ? FlowVmValue.FromNumber(numbers[index], quality)
                : FlowVmValue.FromBoolean(booleans[index] != 0, quality);
        }

        return result;
    }

    private sealed class NativeVmHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public NativeVmHandle() : base(true)
        {
        }

        public NativeVmHandle(IntPtr handle) : base(true) => SetHandle(handle);

        protected override bool ReleaseHandle()
        {
            _ = Native.flow_vm_clear(handle);
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeResult { public int Code; public fixed byte Path[96]; }
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct NativeTarget
    {
        [FieldOffset(0)] public uint AbiVersion;
        [FieldOffset(8)] public ulong Capabilities;
        [FieldOffset(16)] public uint MaximumArtifactBytes;
        [FieldOffset(20)] public uint MaximumWorkPerScan;
        [FieldOffset(24)] public uint MaximumSnapshotBytes;
    }
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    private struct NativeInput
    {
        [FieldOffset(0)] public fixed byte PointId[64];
        [FieldOffset(64)] public byte Value;
        [FieldOffset(65)] public byte Quality;
        [FieldOffset(66)] public byte Type;
        [FieldOffset(72)] public double Number;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInputFrame { public IntPtr Samples; public nuint SampleCount; public ulong SampledAtMilliseconds; public byte IsCoherent; }
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    private struct NativeCommand
    {
        [FieldOffset(0)] public fixed byte PointId[64];
        [FieldOffset(64)] public byte Value;
        [FieldOffset(65)] public byte Quality;
        [FieldOffset(66)] public byte Type;
        [FieldOffset(72)] public double Number;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeExecutionView { public ushort InstructionIndex; public byte Opcode; public byte IsAtCommit; }
    [StructLayout(LayoutKind.Explicit, Size = 8336)]
    private struct NativeDebugFrame
    {
        [FieldOffset(0)] public NativeExecutionView Execution;
        [FieldOffset(4)] public ushort SlotCount;
        [FieldOffset(6)] public ushort StateCount;
        [FieldOffset(8)] public ushort OutputCount;
        [FieldOffset(10)] public fixed byte Slots[MaximumSlots];
        [FieldOffset(266)] public fixed byte CurrentState[MaximumStates];
        [FieldOffset(394)] public fixed byte StagedState[MaximumStates];
        [FieldOffset(522)] public fixed byte StagedStateValid[MaximumStates];
        [FieldOffset(656)] public fixed byte Outputs[MaximumOutputs * 80];
        [FieldOffset(5776)] public fixed byte SlotTypes[MaximumSlots];
        [FieldOffset(6032)] public fixed byte SlotQualities[MaximumSlots];
        [FieldOffset(6288)] public fixed double NumericSlots[MaximumSlots];
    }
    [StructLayout(LayoutKind.Explicit, Size = 8032)]
    private struct NativeSnapshot
    {
        [FieldOffset(0)] public fixed byte FlowId[64];
        [FieldOffset(64)] public uint FlowRevision;
        [FieldOffset(72)] public ulong ScanNumber;
        [FieldOffset(80)] public ulong SampledAtMilliseconds;
        [FieldOffset(88)] public ushort SlotCount;
        [FieldOffset(90)] public ushort OutputCount;
        [FieldOffset(92)] public fixed byte Slots[MaximumSlots];
        [FieldOffset(352)] public fixed byte Outputs[MaximumOutputs * 80];
        [FieldOffset(5472)] public fixed byte SlotTypes[MaximumSlots];
        [FieldOffset(5728)] public fixed byte SlotQualities[MaximumSlots];
        [FieldOffset(5984)] public fixed double NumericSlots[MaximumSlots];
    }

    private static partial class Native
    {
        private const string Library = "flow_vm_shared";
        [LibraryImport(Library)] public static partial uint flow_vm_get_abi_version();
        [LibraryImport(Library)] public static partial nuint flow_vm_get_instance_size();
        [LibraryImport(Library)] public static partial NativeResult flow_vm_prepare(byte* artifact, nuint size, NativeTarget* target, IntPtr vm);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_initialize(IntPtr vm, byte* retainedState, nuint size);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_begin_tick(IntPtr vm, NativeInputFrame* input);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_step_instruction(IntPtr vm, NativeExecutionView* view);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_get_debug_frame(IntPtr vm, NativeDebugFrame* frame);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_commit_tick(IntPtr vm, NativeCommand* commands, nuint capacity, nuint* count, NativeSnapshot* snapshot);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_abort_tick(IntPtr vm);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_reset(IntPtr vm);
        [LibraryImport(Library)] public static partial NativeResult flow_vm_clear(IntPtr vm);
    }
}
