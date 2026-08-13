using Microsoft.Win32.SafeHandles;
using Server.Services.Contracts;
using System.Runtime.InteropServices;
using System.Text;

namespace Server.Services.Implementation;

internal sealed unsafe partial class NativeFlowVirtualMachine : IFlowVirtualMachine
{
    private const uint AbiVersion = 1;
    private const int MaximumArtifactBytes = 8192;
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
                Capabilities = 0x1f,
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
                samples[index].Quality = inputs[index].IsGood ? (byte)1 : (byte)0;
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
                resultCommands[index] = new FlowVmCommand(ReadIdentifier(commands[index].PointId, 64), commands[index].Value != 0);
            }

            var slots = new bool[snapshot.SlotCount];
            for (var index = 0; index < slots.Length; index++) slots[index] = snapshot.Slots[index] != 0;
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
            samples[index].Quality = inputs[index].IsGood ? (byte)1 : (byte)0;
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
            resultCommands[index] = new FlowVmCommand(ReadIdentifier(commands[index].PointId, 64), commands[index].Value != 0);
        }

        var slots = new bool[snapshot.SlotCount];
        for (var index = 0; index < slots.Length; index++) slots[index] = snapshot.Slots[index] != 0;
        return new FlowVmScanResult(snapshot.ScanNumber, snapshot.SampledAtMilliseconds, slots, resultCommands);
    }

    private FlowVmExecutionFrame GetFrameCore()
    {
        NativeDebugFrame frame = default;
        Check(Native.flow_vm_get_debug_frame(Handle, &frame));
        var slots = new bool[frame.SlotCount];
        var currentState = new bool[frame.StateCount];
        var stagedState = new bool?[frame.StateCount];
        var commands = new FlowVmCommand[frame.OutputCount];
        for (var index = 0; index < slots.Length; index++) slots[index] = frame.Slots[index] != 0;
        for (var index = 0; index < currentState.Length; index++)
        {
            currentState[index] = frame.CurrentState[index] != 0;
            stagedState[index] = frame.StagedStateValid[index] != 0 ? frame.StagedState[index] != 0 : null;
        }
        for (var index = 0; index < commands.Length; index++)
        {
            commands[index] = new FlowVmCommand(ReadIdentifier(frame.Outputs + (index * 65), 64), frame.Outputs[(index * 65) + 64] != 0);
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
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput { public fixed byte PointId[64]; public byte Value; public byte Quality; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInputFrame { public IntPtr Samples; public nuint SampleCount; public ulong SampledAtMilliseconds; public byte IsCoherent; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCommand { public fixed byte PointId[64]; public byte Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeExecutionView { public ushort InstructionIndex; public byte Opcode; public byte IsAtCommit; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDebugFrame
    {
        public NativeExecutionView Execution;
        public ushort SlotCount;
        public ushort StateCount;
        public ushort OutputCount;
        public fixed byte Slots[MaximumSlots];
        public fixed byte CurrentState[MaximumStates];
        public fixed byte StagedState[MaximumStates];
        public fixed byte StagedStateValid[MaximumStates];
        public fixed byte Outputs[MaximumOutputs * 65];
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSnapshot
    {
        public fixed byte FlowId[64];
        public uint FlowRevision;
        public ulong ScanNumber;
        public ulong SampledAtMilliseconds;
        public ushort SlotCount;
        public ushort OutputCount;
        public fixed byte Slots[MaximumSlots];
        public fixed byte Outputs[MaximumOutputs * 65];
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
