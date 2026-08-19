namespace Server.Services;

public interface IFlowVirtualMachine : IDisposable
{
    FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds);

    FlowVmExecutionFrame BeginScan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds) =>
        throw new NotSupportedException("The VM host does not support instruction debugging.");

    FlowVmExecutionFrame StepInstruction() =>
        throw new NotSupportedException("The VM host does not support instruction debugging.");

    FlowVmScanResult CommitScan() =>
        throw new NotSupportedException("The VM host does not support instruction debugging.");

    void AbortScan() =>
        throw new NotSupportedException("The VM host does not support instruction debugging.");

    void Reset();
}

public interface IFlowVirtualMachineFactory
{
    IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact);
}